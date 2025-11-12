using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGameLibrary;
using MonoGameLibrary.Graphics;


namespace DungeonSlime.GameObjects;

public class Slime : GameObject
{

    // The AnimatedSprite used when drawing each slime segment
    public AnimatedSprite Sprite { get; private set; }
    public int ColliderId { get; private set; }
    private Vector2 _vel;
    private float _speed;
    public Slime(AnimatedSprite sprite)
    {
        Sprite = sprite;
    }

    public void Initialize()
    {
        _speed = 8.0f;
        Pos = new Vector2(Core.GraphicsDevice.Viewport.Width * 0.5f, Core.GraphicsDevice.Viewport.Height * 0.5f);
        Core.Cam.Position = Pos;
        ColliderId = Core.Cols.CreateCircle(Pos, radius: 60f, layer: 0, new Color(10, 243, 10, 170)); // player - layer 0
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


    private void Move(GameTime t)
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

        Move(gameTime);
        Core.Cols.SetPosition(ColliderId, new Vector2(Pos.X + Sprite.Width * 0.5f, Pos.Y + Sprite.Height * 0.5f));

    }

    /// <summary>
    /// Draws the slime.
    /// </summary>
    public void Draw()
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
}
