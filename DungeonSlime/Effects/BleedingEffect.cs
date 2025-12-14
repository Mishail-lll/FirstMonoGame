using MonoGameLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DungeonSlime.GameObjects;
using Microsoft.Xna.Framework;

namespace DungeonSlime.Effects;

public class BleedingEffect : IEffect
{
    public string Name { get; set; } = "Bleeding";
    public float Power { get; set; } = 0.0f;
    public float Time { get; private set; } = 0;

    public float Duration { get; private set; } = 10f;

    private Player _player { get; set; }

    public BleedingEffect(Player player)
    {
        _player = player;
    }

    public void Update()
    {
        if (Time < Duration)
        {
            Time += Core.Step;
            _player.GetDamage(Power * Core.Step, Player.DamageType.Poison);
        }
        else
        {
            Clear();
        }
    }

    public void Clear()
    {
        Time = 0;
        Power = 0;
        _player.Sprite.Color = Color.White;
    }

    public void Initialize()
    {
        Power = 1;
        _player.Sprite.Color = Color.Red;
    }
}
