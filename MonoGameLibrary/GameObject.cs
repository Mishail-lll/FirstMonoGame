using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace MonoGameLibrary;

public class GameObject
{
    public Vector2 Pos { get; set; }
    public virtual void Update() { }
    public virtual void Draw() { }
}
