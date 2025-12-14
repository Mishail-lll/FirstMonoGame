using Microsoft.Xna.Framework;
using MonoGameLibrary;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Phisics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DungeonSlime.Effects;

public interface IEffect
{
    string Name { get; set; }
    float Power { get; set; }
    public void Update() { }
    public void Clear() { }
    public void Initialize() { }
}
