
using DungeonSlime.Effects;
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

public class StrongSlime : GameObject, IEnemy
{
    public bool Active { get; private set; }
    public Sprite Sprite { get; private set; }
    public IEffect Effect { get; private set; }
    private Player _player;
    public Collider Collider { get; private set; }
    private Vector2 _vel;
    private float _speed;
    public float Damage;
    public StrongSlime()
    {

    }

    public void Initialize(Player player, Sprite sprite, Vector2 pos)
    {
        Sprite = sprite;
        _speed = 1.0f;
        Damage = 2f;
        _player = player;
        Pos = pos;
        Collider = Core.NewCols.CreateCircle(Pos, 28f, 2, new Color(243, 10, 10, 170), this); // enemy - layer 2
        Effect = new BleedingEffect(_player);
    }

    public override void Update()
    {
        // Handle any player input
        Core.NewCols.SetPosition(Collider, Pos);

        _vel = Vector2.Normalize(_player.Pos - Pos);

        Pos += _vel * _speed;
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
            (int)Pos.X,
            (int)Pos.Y,
            (int)(Sprite.Width * 0.5f),
            Color.Red,
            15
        );

        return bounds;
    }

    public void Hit()
    {
        _player.GetEffect(Effect);
        _player.GetDamage(Damage, Player.DamageType.Male);
        Core.Audio.PlaySoundEffectByKey("collect");
        Despawn();
    }

    public int GetId()
    {
        return Collider.Id;
    }

    public void Despawn()
    {
        Active = false;
        //Core.NewCols.RemoveCollider(Collider.Id);
        Core.NewCols.SetActive(Collider, false);
        _vel = Vector2.Zero;
        _speed = 0;
        Damage = 0;
    }

    public void Activate()
    {
        Active = true;
    }
}
