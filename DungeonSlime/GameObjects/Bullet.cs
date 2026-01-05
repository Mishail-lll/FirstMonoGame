using Microsoft.Xna.Framework;
using MonoGameLibrary;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Phisics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DungeonSlime.GameObjects;

public class Bullet : GameObject
{
    public Collider Collider { get; private set; }
    public Sprite Sprite { get; private set; }
    private float Damage;
    private Vector2 _vel;
    private float _speed;
    public void Initialize(Sprite sprite, Vector2 pos)
    {
        Sprite = sprite;
        _speed = 10.0f;
        Damage = 2f;
        _vel = Vector2.UnitX;
        Pos = pos;
        Collider = Core.NewCols.CreateCircle(Pos, 14f, 4, Color.Black, this);
        NewCollisionSystem.AddHandler<IEnemy>(ref Collider.EnterByLayer, 2, e => new Action(e.Despawn));
    }
    public override void Update()
    {
        Pos += _vel * _speed;
        Core.NewCols.SetPosition(Collider, Pos);
    }

    public override void Draw()
    {
        Sprite.Draw(Core.SpriteBatch, Pos);
    }

    public Circle GetBounds()
    {

        // Create the bounds using the calculated visual position of the head.
        Circle bounds = new Circle(
            (int)Pos.X,
            (int)Pos.Y,
            (int)(Sprite.Width * 0.5f),
            Color.Red,
            15
        );

        return bounds;
    }
}