using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGameLibrary;
using MonoGameLibrary.Input;
using MonoGameLibrary.Graphics;


namespace DungeonSlime.GameObjects;

public class Player : GameObject
{

    // The AnimatedSprite used when drawing each slime segment
    public AnimatedSprite Sprite { get; private set; }

    public enum DamageType
    {
        Male
    }
    public int ColliderId { get; private set; }
    public float Hp { get; private set; }
    public float MaxHp { get; private set; }
    private Vector2 _vel;
    private float _speed;
    public Player(AnimatedSprite sprite)
    {
        Sprite = sprite;
    }

    public void Initialize()
    {
        _speed = 8.0f;
        MaxHp = 100;
        Hp = MaxHp;
        Pos = Core.Viewport;
        Core.Cam.Position = Pos;
        ColliderId = Core.Cols.CreateCircle(Pos + Sprite.Scale * 0.5f, 60f, 0, new Color(10, 243, 10, 170)); // player - layer 0
    }

    private void HandleInput()
    {
        _vel = Vector2.Zero;
        _vel += Vector2.UnitY * -Convert.ToInt32(GameController.MoveUp()) +
        Vector2.UnitY * Convert.ToInt32(GameController.MoveDown()) +
        Vector2.UnitX * -Convert.ToInt32(GameController.MoveLeft()) +
        Vector2.UnitX * Convert.ToInt32(GameController.MoveRight());
        _vel -= (_vel * Convert.ToInt32(_vel.X != 0 && _vel.Y != 0) * 0.27f);
    }


    private void Move()
    {
        Pos += _vel * _speed;
        Core.Cam.Position = Pos;
    }

    /// <summary>
    /// Updates the slime.
    /// </summary>
    /// <param name="gameTime">A snapshot of the timing values for the current update cycle.</param>
    public void Update(GameTime gameTime)
    {
        // Update the animated sprite.
        Sprite.Update(gameTime);

        // Handle any player input
        HandleInput();

        Move();
        Core.Cols.SetPosition(ColliderId, Pos);

    }

    /// <summary>
    /// Draws the slime.
    /// </summary>
    public override void Draw()
    {
        Sprite.Draw(Core.SpriteBatch, Pos);

    }

    public Circle GetBounds()
    {

        // Create the bounds using the calculated visual position of the head.
        Circle bounds = new Circle(
            (int)(Pos.X + (Sprite.Width * 0.5f)),
            (int)(Pos.Y + (Sprite.Height * 0.5f)),
            (int)(Sprite.Width * 0.5f),
            new Color(10, 243, 10, 170),
            15
        );

        return bounds;
    }

    public void GetDamage(float Damage, DamageType type)
    {
        Hp -= Damage;
    }
}
