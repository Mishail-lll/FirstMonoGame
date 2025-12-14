using DungeonSlime.GameObjects;
using MonoGameLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace DungeonSlime.Effects
{
    public class NauseaEffect : IEffect
    {
        public string Name { get; set; } = "Nausea";
        public float Power { get; set; } = 0.0f;
        public float Time { get; private set; } = 0;
        public float Smooth { get; private set; } = 0.0f;
        public float Duration { get; private set; } = 10f;

        private Player _player { get; set; }

        public NauseaEffect(Player player)
        {
            _player = player;
        }

        public void Update()
        {
            Time += Core.Step;
            if (Time < Duration)
            {
                Smooth += Core.Step * 0.2f;
                Smooth = Math.Min(Smooth, 1);
            }
            else
            {
                Smooth -= Core.Step * 0.2f;
                Smooth = Math.Max(Smooth, 0);
            }
            Core.Cam.Rotation = (float)Math.Cos(Time) * Power * Smooth;
            Core.Cam.ZoomX = 1f - (float)Math.Cos(Time) * Power * Smooth;
            Core.Cam.ZoomY = 1f - (float)Math.Sin(Time) * Power * Smooth;
            if (Smooth <= 0)
            {
                Clear();
            }
        }

        public void Clear()
        {
            Time = 0;
            Smooth = 0;
            Power = 0;
            Core.Cam.ZoomX = 1;
            Core.Cam.ZoomY = 1;
            Core.Cam.Rotation = 0;
            _player.Sprite.Color = Color.White;
        }

        public void Initialize()
        {
            Power = 0.2f;
            _player.Sprite.Color = Color.Green;
        }
    }
}
