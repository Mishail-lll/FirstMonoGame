using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using static MonoGameLibrary.Phisics.Collider;

namespace MonoGameLibrary.Phisics;
public sealed class NewCollisionSystem
{
    // ===== данные =====
    private readonly List<Collider> _colliders = new();
    private readonly HashSet<Collider> _dirty = new();

    // состояние прошлых столкновений
    private readonly Dictionary<ulong, bool> _collisionStates = new();

    // ===== public API: создание =====

    public Collider CreateCircle(
        Vector2 position,
        float radius,
        int layer,
        Color color,
        object owner)
    {
        var col = new Collider(
            position,
            new Vector2(radius, radius),
            layer,
            color,
            ColliderShape.Circle,
            owner
        );

        _colliders.Add(col);
        _dirty.Add(col);
        return col;
    }

    public Collider CreateAABB(
        Vector2 position,
        Vector2 haalfSize,
        int layer,
        Color color,
        object owner)
    {
        var col = new Collider(
            position,
            haalfSize,
            layer,
            color,
            ColliderShape.AABB,
            owner
        );

        _colliders.Add(col);
        _dirty.Add(col);
        return col;
    }

    public void RemoveCollider(Collider col)
    {
        if (col == null)
            return;

        // 1. Сгенерировать Exit для всех активных столкновений
        for (int i = 0; i < _colliders.Count; i++)
        {
            var other = _colliders[i];
            if (other == col || !other.Active)
                continue;

            ulong key = MakePairKey(col, other);
            if (_collisionStates.ContainsKey(key))
            {
                InvokeExit(col, other);
                InvokeExit(other, col);
                _collisionStates.Remove(key);
            }
        }

        // 2. Удалить из систем
        col.Active = false;
        _dirty.Remove(col);
        _colliders.Remove(col);
    }

    public void RemoveAll()
    {
        _collisionStates.Clear();
        _dirty.Clear();
        _colliders.Clear();
    }

    public void UnregisterAllHendlers()
    {
        foreach (var col in _colliders)
        {
            col.EnterById?.Clear();
            col.ExitById?.Clear();
            col.StayById?.Clear();

            col.EnterByLayer?.Clear();
            col.ExitByLayer?.Clear();
            col.StayByLayer?.Clear();
        }
    }

    // ===== управление состоянием =====

    public void SetActive(Collider col, bool active)
    {
        if (col.Active == active) return;
        if (!active)
            ForceExit(col); // НОВОЕ
        col.Active = active;
        _dirty.Add(col);
    }

    private void ForceExit(Collider col)
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            var other = _colliders[i];
            if (other == col || !other.Active) continue;

            ulong key = MakePairKey(col, other);
            if (_collisionStates.Remove(key))
            {
                InvokeExit(col, other);
                InvokeExit(other, col);
            }
        }
    }


    public void SetPosition(Collider col, Vector2 pos)
    {
        if (col.Position == pos) return;
        col.Position = pos;
        _dirty.Add(col);
    }

    public void SetHalfSize(Collider col, Vector2 halfSize)
    {
        if (col.HalfSize == halfSize) return;
        col.HalfSize = halfSize;
        _dirty.Add(col);
    }

    public void SetColor(Collider col, Color color)
    {
        if (col.Color == color) return;
        col.Color = color;
    }

    // ===== регистрация событий (пример: Enter by Layer) =====

    public static void AddHandler<TClass>(
    ref Dictionary<int, List<Func<object, Action>>> map,
    int key,
    Func<TClass, Action> methodSelector)
    where TClass : class
    {
        map ??= new Dictionary<int, List<Func<object, Action>>>();

        if (!map.TryGetValue(key, out var list))
        {
            list = new List<Func<object, Action>>();
            map[key] = list;
        }

        Func<object, Action> wrapper = ownerObj =>
        {
            var typed = ownerObj as TClass;
            return typed != null ? methodSelector(typed) : null;
        };

        list.Add(wrapper);
    }

    public static void RemoveHandler(
    Dictionary<int, List<Func<object, Action>>> map,
    int key)
    {
        map?.Remove(key);
    }

    // ===== Update =====

    public void Update()
    {
        if (_dirty.Count == 0)
            return;

        foreach (var a in _dirty)
        {
            if (!a.Active) continue;

            foreach (var b in _colliders)
            {
                if (a == b || !b.Active) continue;

                ulong key = MakePairKey(a, b);
                bool wasColliding = _collisionStates.ContainsKey(key);
                bool isColliding = Intersects(a, b);

                if (!wasColliding && isColliding)
                {
                    InvokeEnter(a, b);
                    InvokeEnter(b, a);
                    _collisionStates[key] = true;
                }
                else if (wasColliding && isColliding)
                {
                    InvokeStay(a, b);
                    InvokeStay(b, a);
                }
                else if (wasColliding && !isColliding)
                {
                    InvokeExit(a, b);
                    InvokeExit(b, a);
                    _collisionStates.Remove(key);
                }
            }
        }

        _dirty.Clear();
    }

    // ===== вызовы событий =====

    private static void InvokeEnter(Collider a, Collider b)
    {
        Invoke(a.EnterById, b.Id, b.Owner);
        Invoke(a.EnterByLayer, b.Layer, b.Owner);
    }

    private static void InvokeStay(Collider a, Collider b)
    {
        Invoke(a.StayById, b.Id, b.Owner);
        Invoke(a.StayByLayer, b.Layer, b.Owner);
    }

    private static void InvokeExit(Collider a, Collider b)
    {
        Invoke(a.ExitById, b.Id, b.Owner);
        Invoke(a.ExitByLayer, b.Layer, b.Owner);
    }

    private static void Invoke(
        Dictionary<int, List<Func<object, Action>>> map,
        int key,
        object owner)
    {
        if (map == null) return;
        if (!map.TryGetValue(key, out var list)) return;

        foreach (var factory in list)
        {
            var action = factory(owner);
            action?.Invoke();
        }
    }

    // ===== геометрия =====

    private static bool Intersects(Collider a, Collider b)
    {
        if (a.Shape == ColliderShape.Circle && b.Shape == ColliderShape.Circle)
            return CircleCircle(a, b);

        if (a.Shape == ColliderShape.AABB && b.Shape == ColliderShape.AABB)
            return AABBAABB(a, b);

        if (a.Shape == ColliderShape.Circle)
            return CircleAABB(a, b);

        return CircleAABB(b, a);
    }

    private static bool CircleCircle(Collider a, Collider b)
    {
        float r = a.HalfSize.X + b.HalfSize.X;
        return Vector2.DistanceSquared(a.Position, b.Position) <= r * r;
    }

    private static bool AABBAABB(Collider a, Collider b)
    {
        return Math.Abs(a.Position.X - b.Position.X) <= (a.HalfSize.X + b.HalfSize.X) &&
               Math.Abs(a.Position.Y - b.Position.Y) <= (a.HalfSize.Y + b.HalfSize.Y);
    }

    private static bool CircleAABB(Collider c, Collider b)
    {
        Vector2 diff = c.Position - b.Position;
        Vector2 clamped = Vector2.Clamp(
            diff,
            -b.HalfSize,
            b.HalfSize
        );
        Vector2 closest = b.Position + clamped;
        return Vector2.DistanceSquared(c.Position, closest) <= c.HalfSize.X * c.HalfSize.X;
    }

    // ===== utils =====

    private static ulong MakePairKey(Collider a, Collider b)
    {
        uint idA = (uint)Math.Min(a.Id, b.Id);
        uint idB = (uint)Math.Max(a.Id, b.Id);
        return ((ulong)idA << 32) | idB;
    }
}
