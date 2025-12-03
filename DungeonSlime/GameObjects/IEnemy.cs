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

public interface IEnemy
{
    bool Active { get; }
    public void Initialize()
    {
        Core.Cols.RegisterHandler(0, 2, (in CollisionSystem.CollisionInfo info) => Hit());
    }

    void Activate() { }

    public void Update() { }


    public void Draw() { }

    public Circle GetBounds()
    {
        return new Circle();
    }

    public int GetId()
    {
        return 0;
    }

    void Hit()
    {

    }
}
