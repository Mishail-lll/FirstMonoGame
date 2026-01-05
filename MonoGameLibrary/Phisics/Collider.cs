using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoGameLibrary.Phisics
{
    public sealed class Collider
    {
        static int NextId;

        public int Id { get; }
        public int Layer { get; }

        public enum ColliderShape : byte { Circle = 0, AABB = 1 }

        public ColliderShape Shape { get; }

        public Color Color { get; internal set; }

        public object Owner { get; }

        public bool Active { get; internal set; } = true;

        public Vector2 Position { get; internal set; }
        public Vector2 HalfSize { get; internal set; } // используется и для Circle, и для AABB

        public Dictionary<int, List<Func<object, Action>>> EnterById;
        public Dictionary<int, List<Func<object, Action>>> StayById;
        public Dictionary<int, List<Func<object, Action>>> ExitById;

        public Dictionary<int, List<Func<object, Action>>> EnterByLayer;
        public Dictionary<int, List<Func<object, Action>>> StayByLayer;
        public Dictionary<int, List<Func<object, Action>>> ExitByLayer;

        public Collider(
            Vector2 position,
            Vector2 halfSize,
            int layer,
            Color color,
            ColliderShape shape,
            object owner
        )
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Active = true;
            Id = NextId++;
            Layer = layer;
            Shape = shape;
            Color = color;
            Position = position;
            HalfSize = halfSize;

            // события инициализируются лениво
            EnterById = null;
            StayById = null;
            ExitById = null;

            EnterByLayer = null;
            StayByLayer = null;
            ExitByLayer = null;
        }

        public Circle GetBounds()
        {
            if (Active)
                return new Circle((int)Position.X, (int)Position.Y, (int)HalfSize.X, Color, 15);
            else
                return Circle.Empty;
        }
    }
}