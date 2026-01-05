using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

/// <summary>
/// CollisionSystem — лёгкий менеджер коллайдеров для MonoGame.
/// Поддерживает Circle и Axis-Aligned Box коллайдеры (AABB),
/// уровни (layers) 0..(MaxLayers-1) и матрицу подписок (callbacks) для каждой пары уровней.
/// 
/// Цели:
/// - минимальные аллокации в hot-path;
/// - пропускать проверки пар уровней, на которые нет подписок;
/// - удобный API для создания/обновления/удаления коллайдеров;
/// - возвращать id (индекс) для доступа к коллайдеру.
/// </summary>

namespace MonoGameLibrary.Phisics
{

    public class CollisionSystem
    {
        // ---------- Public API Types ----------

        /// <summary>Тип формы коллайдера.</summary>
        public enum Shape : byte { Circle = 0, Box = 1 }

        /// <summary>Информация о столкновении, передаваемая в обработчик.</summary>
        public struct CollisionInfo
        {
            public int IdA;
            public int IdB;
            public int LayerA;
            public int LayerB;
            public Shape ShapeA;
            public Shape ShapeB;
        }

        /// <summary>Колбек, вызываемый при коллизии. Получает CollisionInfo.</summary>
        public delegate void CollisionHandler(in CollisionInfo info);

        /// <summary>Структура-коллайдер — value type, хранится в массиве.</summary>
        public struct Collider
        {
            public int Id;               // уникальный id (индекс в массиве)
            public bool Active;         // можно временно отключать
            public Shape Shape;         // Circle или Box
            public int Layer;           // 0..MaxLayers-1
            public Vector2 Center;      // позиция (мировые координаты)
            public Color Color;
            // circle
            public float Radius;

            // box (axis-aligned)
            public Vector2 HalfSize;

            // helper: AABB
            public void GetAABB(out Vector2 min, out Vector2 max)
            {
                if (Shape == Shape.Circle)
                {
                    min = new Vector2(Center.X - Radius, Center.Y - Radius);
                    max = new Vector2(Center.X + Radius, Center.Y + Radius);
                }
                else
                {
                    min = Center - HalfSize;
                    max = Center + HalfSize;
                }
            }
        }

        // ---------- Configuration & Storage ----------

        /// <summary>Максимум уровней по умолчанию (0..4 => 5 уровней).</summary>
        public const int DefaultMaxLayers = 5;
        readonly int _maxLayers;

        // id -> owner (создатель коллайдера). Хранишь экземпляры там.
        readonly Dictionary<int, object> _ownerById = new Dictionary<int, object>();

        // per-layer: layer -> (id -> list of handler entries)
        Dictionary<int, List<Func<object, Action>>>[] _instanceHandlerMaps;

        bool[] _layerHandlerNonFalseOnLayer; // длина = _maxLayers

        // callbacks matrix and fast check
        CollisionHandler[,] _callbacks;
        bool[,] _hasCallback;

        // enter/exit handlers
        CollisionHandler[,] _enterHandlers;
        CollisionHandler[,] _exitHandlers;
        bool[,] _hasEnterHandler;
        bool[,] _hasExitHandler;

        // pair tracking between frames
        private System.Collections.Generic.HashSet<long> _prevPairs = new System.Collections.Generic.HashSet<long>();
        private System.Collections.Generic.HashSet<long> _currPairs = new System.Collections.Generic.HashSet<long>();


        // colliders storage: array for ref access and minimal GC.
        Collider[] _colliders;
        int _count;                  // количество занятых слотов (включая неактивные)
        int[] _freeStack;   // стек свободных индексов для reuse
        int _freeCount;
        int _initialCapacity;

        // Конструктор
        public CollisionSystem(int initialCapacity = 256, int maxLayers = DefaultMaxLayers)
        {
            if (initialCapacity <= 0) initialCapacity = 16;
            if (maxLayers <= 0 || maxLayers > 32) throw new ArgumentOutOfRangeException(nameof(maxLayers));

            _maxLayers = maxLayers;

            _instanceHandlerMaps = new Dictionary<int, List<Func<object, Action>>>[_maxLayers];
            for (int i = 0; i < _maxLayers; i++) _instanceHandlerMaps[i] = new Dictionary<int, List<Func<object, Action>>>();

            _layerHandlerNonFalseOnLayer = new bool[_maxLayers];

            _callbacks = new CollisionHandler[_maxLayers, _maxLayers];
            _hasCallback = new bool[_maxLayers, _maxLayers];
            _enterHandlers = new CollisionHandler[_maxLayers, _maxLayers];
            _exitHandlers = new CollisionHandler[_maxLayers, _maxLayers];
            _hasEnterHandler = new bool[_maxLayers, _maxLayers];
            _hasExitHandler = new bool[_maxLayers, _maxLayers];


            _colliders = new Collider[initialCapacity];
            _freeStack = new int[initialCapacity];
            _count = 0;
            _freeCount = 0;
            _initialCapacity = initialCapacity;
        }

        // ---------- Public API: Subscriptions ----------

        // Регистрация метода интерфейса без аргументов: e => e.Hit()
        public void RegisterHandlerByLayer<TClass>(
        int layer,
        int id,
        Func<TClass, Action> methodSelector
    ) where TClass : class
        {
            ValidateLayer(layer);

            if (methodSelector == null)
                throw new ArgumentNullException(nameof(methodSelector));

            var map = _instanceHandlerMaps[layer];

            if (!map.TryGetValue(id, out var list))
            {
                list = new List<Func<object, Action>>();
                map[id] = list;
            }

            // Обёртка: ownerObj -> Action
            Func<object, Action> wrapper = ownerObj =>
            {
                // Кастуем владельца к интерфейсу
                var typed = ownerObj as TClass;
                if (typed == null)
                    return null; // владелец не того типа — обработчик не подходит

                // Вызываем селектор и получаем метод конкретного экземпляра
                return methodSelector(typed);
            };

            list.Add(wrapper);
            _layerHandlerNonFalseOnLayer[layer] = true;
        }



        /// <summary>Register handler for pair of levels. Registration is symmetric: (a,b) and (b,a).</summary>
        public void RegisterHandler(int layerA, int layerB, CollisionHandler handler)
        {
            ValidateLayer(layerA); ValidateLayer(layerB);
            if (layerA > layerB)
                (layerA, layerB) = (layerB, layerA);
            _callbacks[layerA, layerB] += handler;
            //if (layerA != layerB) _callbacks[layerB, layerA] += handler;
            UpdateHasCallback(layerA, layerB);
        }

        /// <summary>Unregister handler for pair of levels.</summary>
        public void UnregisterHandler(int layerA, int layerB, CollisionHandler handler)
        {
            ValidateLayer(layerA); ValidateLayer(layerB);
            if (layerA > layerB)
                (layerA, layerB) = (layerB, layerA);
            _callbacks[layerA, layerB] -= handler;
            //if (layerA != layerB) _callbacks[layerB, layerA] -= handler;
            UpdateHasCallback(layerA, layerB);
        }

        public void UnregisterAllHendlers()
        {
            _callbacks = new CollisionHandler[_maxLayers, _maxLayers];
            _enterHandlers = new CollisionHandler[_maxLayers, _maxLayers];
            _exitHandlers = new CollisionHandler[_maxLayers, _maxLayers];
            _hasCallback = new bool[_maxLayers, _maxLayers];
            _hasEnterHandler = new bool[_maxLayers, _maxLayers];
            _hasExitHandler = new bool[_maxLayers, _maxLayers];
            _layerHandlerNonFalseOnLayer = new bool[_maxLayers];
            _instanceHandlerMaps = new Dictionary<int, List<Func<object, Action>>>[_maxLayers];
            for (int i = 0; i < _maxLayers; i++) _instanceHandlerMaps[i] = new Dictionary<int, List<Func<object, Action>>>();
        }

        void UpdateHasCallback(int a, int b)
        {
            if (a > b)
                (a, b) = (b, a);
            _hasCallback[a, b] = _callbacks[a, b] != null;
            //if (a != b) _hasCallback[b, a] = _callbacks[b, a] != null;
        }

        // Register/Unregister enter handlers (symmetric)
        public void RegisterEnterHandler(int layerA, int layerB, CollisionHandler handler)
        {
            ValidateLayer(layerA); ValidateLayer(layerB);
            if (layerA > layerB)
                (layerA, layerB) = (layerB, layerA);
            _enterHandlers[layerA, layerB] += handler;
            //if (layerA != layerB) _enterHandlers[layerB, layerA] += handler;
            _hasEnterHandler[layerA, layerB] = _enterHandlers[layerA, layerB] != null;
            //if (layerA != layerB) _hasEnterHandler[layerB, layerA] = _enterHandlers[layerB, layerA] != null;
        }

        public void UnregisterEnterHandler(int layerA, int layerB, CollisionHandler handler)
        {
            ValidateLayer(layerA); ValidateLayer(layerB);
            if (layerA > layerB)
                (layerA, layerB) = (layerB, layerA);
            _enterHandlers[layerA, layerB] -= handler;
            //if (layerA != layerB) _enterHandlers[layerB, layerA] -= handler;
            _hasEnterHandler[layerA, layerB] = _enterHandlers[layerA, layerB] != null;
            //if (layerA != layerB) _hasEnterHandler[layerB, layerA] = _enterHandlers[layerB, layerA] != null;
        }

        // Register/Unregister exit handlers (symmetric)
        public void RegisterExitHandler(int layerA, int layerB, CollisionHandler handler)
        {
            ValidateLayer(layerA); ValidateLayer(layerB);
            if (layerA > layerB)
                (layerA, layerB) = (layerB, layerA);
            _exitHandlers[layerA, layerB] += handler;
            //if (layerA != layerB) _exitHandlers[layerB, layerA] += handler;
            _hasExitHandler[layerA, layerB] = _exitHandlers[layerA, layerB] != null;
            //if (layerA != layerB) _hasExitHandler[layerB, layerA] = _exitHandlers[layerB, layerA] != null;
        }

        public void UnregisterExitHandler(int layerA, int layerB, CollisionHandler handler)
        {
            ValidateLayer(layerA); ValidateLayer(layerB);
            if (layerA > layerB)
                (layerA, layerB) = (layerB, layerA);
            _exitHandlers[layerA, layerB] -= handler;
            //if (layerA != layerB) _exitHandlers[layerB, layerA] -= handler;
            _hasExitHandler[layerA, layerB] = _exitHandlers[layerA, layerB] != null;
            //if (layerA != layerB) _hasExitHandler[layerB, layerA] = _exitHandlers[layerB, layerA] != null;
        }


        /// <summary>Clear all handlers.</summary>
        public void ClearHandlers()
        {
            for (int i = 0; i < _maxLayers; i++)
                for (int j = 0; j < _maxLayers; j++)
                {
                    _callbacks[i, j] = null;
                    _hasCallback[i, j] = false;
                }
        }

        // ---------- Public API: create/update/remove colliders ----------

        /// <summary>Create a circle collider and return its id.</summary>
        public int CreateCircle(Vector2 center, float radius, int layer, Color color, object obj, bool active = true)
        {
            ValidateLayer(layer);
            if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
            var c = new Collider
            {
                Active = active,
                Shape = Shape.Circle,
                Layer = layer,
                Center = center,
                Radius = radius,
                Color = color
            };
            int id = AddCollider(c);
            _ownerById[id] = obj;
            return id;
        }

        /// <summary>Create an axis-aligned box (halfSize = half-width/half-height) and return id.</summary>
        public int CreateBox(Vector2 center, Vector2 halfSize, int layer, object obj, bool active = true)
        {
            ValidateLayer(layer);
            if (halfSize.X <= 0 || halfSize.Y <= 0) throw new ArgumentOutOfRangeException(nameof(halfSize));
            var c = new Collider
            {
                Active = active,
                Shape = Shape.Box,
                Layer = layer,
                Center = center,
                HalfSize = halfSize
            };
            int id = AddCollider(c);
            _ownerById[id] = obj;
            return id;
        }

        int AddCollider(Collider c)
        {
            int id;
            if (_freeCount > 0)
            {
                // reuse free slot
                id = _freeStack[--_freeCount];
                c.Id = id;
                _colliders[id] = c; // assign whole struct
            }
            else
            {
                // grow if needed
                if (_count >= _colliders.Length) Grow(_colliders.Length * 2);
                id = _count;
                c.Id = id;
                _colliders[_count++] = c;
            }
            return id;
        }

        void Grow(int newSize)
        {
            if (newSize <= _colliders.Length) newSize = _colliders.Length * 2;
            Array.Resize(ref _colliders, newSize);
            Array.Resize(ref _freeStack, newSize);
        }

        /// <summary>Remove collider (free slot). Does not compact array immediately.</summary>
        public void RemoveCollider(int id)
        {
            if (!ValidId(id)) return;
            // mark inactive and push to free stack
            var tmp = _colliders[id];
            tmp.Active = false;
            _colliders[id] = tmp;
            _freeStack[_freeCount++] = id;
        }

        /// <summary>Set collider active/inactive.</summary>
        public void SetActive(int id, bool active)
        {
            if (!ValidId(id)) return;
            var tmp = _colliders[id];
            tmp.Active = active;
            _colliders[id] = tmp;
        }

        /// <summary>Set collider world position.</summary>
        public void SetPosition(int id, Vector2 center)
        {
            if (!ValidId(id)) return;
            var tmp = _colliders[id];
            tmp.Center = center;
            _colliders[id] = tmp;
        }

        public void SetColor(int id, Color color)
        {
            if (!ValidId(id)) return;
            var tmp = _colliders[id];
            tmp.Color = color;
            _colliders[id] = tmp;
        }

        /// <summary>Set circle radius.</summary>
        public void SetRadius(int id, float radius)
        {
            if (!ValidId(id)) return;
            var tmp = _colliders[id];
            if (tmp.Shape != Shape.Circle) throw new InvalidOperationException("Not a circle");
            tmp.Radius = radius;
            _colliders[id] = tmp;
        }

        public void RemoveAll()
        {
            for (int i = 0; i < _count; i++)
                _colliders[i].Active = false;
            _freeCount = 0;
            for (int i = 0; i < _count; i++) _freeStack[_freeCount++] = i;
        }

        public static bool NarrowPhase(Collider A, Collider B)
        {
            Vector2 amin, amax, bmin, bmax;
            A.GetAABB(out amin, out amax);
            B.GetAABB(out bmin, out bmax);
            if (amax.X < bmin.X || amin.X > bmax.X || amax.Y < bmin.Y || amin.Y > bmax.Y) return false;

            if (A.Shape == Shape.Circle)
            {
                if (B.Shape == Shape.Circle) return CircleVsCircle(in A, in B);
                else return CircleVsBox(in A, in B);
            }
            else
            {
                if (B.Shape == Shape.Circle) return CircleVsBox(in B, in A);
                else return BoxVsBox(in A, in B);
            }
        }

        /// <summary>Set box half-size.</summary>
        public void SetHalfSize(int id, Vector2 halfSize)
        {
            if (!ValidId(id)) return;
            var tmp = _colliders[id];
            if (tmp.Shape != Shape.Box) throw new InvalidOperationException("Not a box");
            tmp.HalfSize = halfSize;
            _colliders[id] = tmp;
        }

        /// <summary>Get a copy of collider (safe read).</summary>
        public Collider GetCollider(int id)
        {
            if (!ValidId(id)) throw new ArgumentOutOfRangeException(nameof(id));
            return _colliders[id];
        }

        /// <summary>Try get collider as ref for heavy users (only valid until next Grow/Remove operations that may reallocate).</summary>
        public ref Collider GetColliderRef(int id)
        {
            if (!ValidId(id)) throw new ArgumentOutOfRangeException(nameof(id));
            return ref _colliders[id];
        }

        public bool ValidId(int id) => id >= 0 && id < _count && !_freeContains(id);

        public Circle GetBounds(int id)
        {
            if (!ValidId(id)) throw new ArgumentOutOfRangeException(nameof(id));
            Collider col = GetCollider(id);
            return new Circle((int)col.Center.X, (int)col.Center.Y, (int)col.Radius, col.Color, 15);
        }
        bool _freeContains(int id)
        {
            // linear scan of small free stack; cheap when freeCount small.
            for (int i = 0; i < _freeCount; i++) if (_freeStack[i] == id) return true;
            return false;
        }

        // ---------- Collision Processing ----------

        /// <summary>
        /// Process collisions for all active colliders.
        /// Algorithm: O(n^2) narrow-phase but _skips_ any pair whose layers have no handler registered.
        /// For many objects use spatial partitioning (not implemented here) — we keep API minimal and clear.
        /// </summary>
        /// <summary>
        /// Простая, надёжная реализация ProcessCollisions:
        /// - собирает пары столкновений в _currPairs
        /// - вызывает per-frame handlers (как раньше)
        /// - затем генерирует enter (curr \ prev) и exit (prev \ curr)
        /// - обновляет _prevPairs
        /// </summary>
        public void ProcessCollisions()
        {
            // очистить текущие пары на старте кадра
            _currPairs.Clear();

            int n = _count;
            for (int i = 0; i < n; i++)
            {
                ref var A = ref _colliders[i];
                if (!A.Active) continue;

                for (int j = i + 1; j < n; j++)
                {
                    ref var B = ref _colliders[j];
                    if (!B.Active) continue;


                    // быстрый фильтр по уровням: если для этой пары уровней никто не подписан - пропустить
                    if (!_hasCallback[B.Layer, A.Layer] && !_hasEnterHandler[B.Layer, A.Layer] && !_hasExitHandler[B.Layer, A.Layer] && !_hasCallback[A.Layer, B.Layer] && !_hasEnterHandler[A.Layer, B.Layer] && !_hasExitHandler[A.Layer, B.Layer] && !_layerHandlerNonFalseOnLayer[A.Layer] && !_layerHandlerNonFalseOnLayer[B.Layer]) continue;

                    if (!NarrowPhase(A, B)) continue;

                    // track pair
                    long key = PairKey(A.Id, B.Id);
                    _currPairs.Add(key);


                    // build info for per-frame handlers
                    var info1 = new CollisionInfo
                    {
                        IdA = A.Id,
                        IdB = B.Id,
                        LayerA = A.Layer,
                        LayerB = B.Layer,
                        ShapeA = A.Shape,
                        ShapeB = B.Shape
                    };

                    // call per-frame handlers safely (if any)
                    if (_hasCallback[A.Layer, B.Layer])
                    {
                        var cb = _callbacks[A.Layer, B.Layer];
                        Debug.WriteLine($"|{A.Layer}| - |{B.Layer}|");
                        try { cb?.Invoke(info1); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Collision handler threw: {ex}"); }
                    }

                    var info2 = new CollisionInfo
                    {
                        IdA = B.Id,
                        IdB = A.Id,
                        LayerA = B.Layer,
                        LayerB = A.Layer, 
                        ShapeA = B.Shape,
                        ShapeB = A.Shape
                    };

                    if (_hasCallback[B.Layer, A.Layer])
                    {
                        var cb = _callbacks[B.Layer, A.Layer];
                        Debug.WriteLine($"|{B.Layer}| - |{A.Layer}|");
                        try { cb?.Invoke(info2); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Collision handler threw: {ex}"); }
                    }

                    //----------------------------------------NEW--------------------------------------------------//
                    //for A layer
                    if (_layerHandlerNonFalseOnLayer[A.Layer])
                    {
                        if (_instanceHandlerMaps[A.Layer].TryGetValue(B.Id, out var list))
                        {
                            var ownerB = _ownerById[A.Id];

                            foreach (var factory in list)
                            {
                                var act = factory(ownerB); // создаём action для этого владельца
                                act?.Invoke();             // вызываем
                            }
                        }
                    }
                    //for B layer
                    if (_layerHandlerNonFalseOnLayer[B.Layer])
                    {
                        if (_instanceHandlerMaps[B.Layer].TryGetValue(A.Id, out var list))
                        {
                            var ownerA = _ownerById[B.Id];

                            foreach (var factory in list)
                            {
                                var act = factory(ownerA); // создаём action для этого владельца
                                act?.Invoke();             // вызываем
                            }
                        }
                    }
                }
            }

            // --- ENTER: pairs in curr but not in prev ---
            foreach (var key in _currPairs)
            {
                if (!_prevPairs.Contains(key))
                {
                    UnpackKey(key, out int id1, out int id2);
                    if (!ValidId(id1) || !ValidId(id2)) continue;
                    var a = _colliders[id1];
                    var b = _colliders[id2];
                    if(!a.Active || !b.Active) continue;
                    CollisionInfo info;

                    if (a.Layer > b.Layer)
                        (a, b) = (b, a);

                    info = new CollisionInfo
                    {
                        IdA = a.Id,
                        IdB = b.Id,
                        LayerA = a.Layer,
                        LayerB = b.Layer,
                        ShapeA = a.Shape,
                        ShapeB = b.Shape
                    };
                    if (_hasEnterHandler[info.LayerA, info.LayerB])
                    {
                        Debug.WriteLine(info.LayerA + " " + info.LayerB);
                        var h = _enterHandlers[info.LayerA, info.LayerB];
                        try { h?.Invoke(info); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Enter handler threw: {ex}"); }
                    }
                }
            }

            // --- EXIT: pairs in prev but not in curr ---
            foreach (var key in _prevPairs)
            {
                if (!_currPairs.Contains(key))
                {
                    UnpackKey(key, out int id1, out int id2);
                    // it's possible one of ids became invalid (removed this frame) — guard
                    if (id1 < 0 || id2 < 0 || id1 >= _count || id2 >= _count) continue;
                    var a = _colliders[id1];
                    var b = _colliders[id2];
                    if (!a.Active || !b.Active) continue;
                    if (a.Layer > b.Layer)
                        (a, b) = (b, a);

                    var info = new CollisionInfo
                    {
                        IdA = a.Id,
                        IdB = b.Id,
                        LayerA = a.Layer,
                        LayerB = b.Layer,
                        ShapeA = a.Shape,
                        ShapeB = b.Shape
                    };

                    if (_hasExitHandler[info.LayerA, info.LayerB])
                    {
                        Debug.WriteLine(info.LayerA + " " + info.LayerB);
                        var h = _exitHandlers[info.LayerA, info.LayerB];
                        try { h?.Invoke(info); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Exit handler threw: {ex}"); }
                    }
                }
            }

            // swap: prev = curr
            _prevPairs.Clear();
            foreach (var k in _currPairs) _prevPairs.Add(k);
            _currPairs.Clear();
        }

        // normalize pair to single long key (unordered)
        private static long PairKey(int idA, int idB)
        {
            if (idA <= idB)
                return ((long)idA << 32) | (uint)idB;
            else
                return ((long)idB << 32) | (uint)idA;
        }

        private static void UnpackKey(long key, out int idA, out int idB)
        {
            idA = (int)(key >> 32);
            idB = (int)(key & 0xFFFFFFFF);
        }


        // ---------- Narrow-phase helper implementations (no sqrt) ----------
        static bool CircleVsCircle(in Collider a, in Collider b)
        {
            float dx = a.Center.X - b.Center.X;
            float dy = a.Center.Y - b.Center.Y;
            float r = a.Radius + b.Radius;
            return dx * dx + dy * dy <= r * r;
        }

        static bool BoxVsBox(in Collider a, in Collider b)
        {
            if (Math.Abs(a.Center.X - b.Center.X) > (a.HalfSize.X + b.HalfSize.X)) return false;
            if (Math.Abs(a.Center.Y - b.Center.Y) > (a.HalfSize.Y + b.HalfSize.Y)) return false;
            return true;
        }

        static bool CircleVsBox(in Collider c, in Collider b)
        {
            float closestX = Math.Max(b.Center.X - b.HalfSize.X, Math.Min(c.Center.X, b.Center.X + b.HalfSize.X));
            float closestY = Math.Max(b.Center.Y - b.HalfSize.Y, Math.Min(c.Center.Y, b.Center.Y + b.HalfSize.Y));
            float dx = c.Center.X - closestX;
            float dy = c.Center.Y - closestY;
            return dx * dx + dy * dy <= c.Radius * c.Radius;
        }

        // ---------- Utilities ----------
        void ValidateLayer(int layer)
        {
            if (layer < 0 || layer >= _maxLayers) throw new ArgumentOutOfRangeException(nameof(layer));
        }

        /// <summary>Return current allocated capacity.</summary>
        public int Capacity => _colliders.Length;

        /// <summary>Return current total count (including free slots which may exist).</summary>
        public int Count => _count;

        /// <summary>Return true if layer pair has any registered handler.</summary>
    }
}