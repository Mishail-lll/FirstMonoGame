using DungeonSlime.GameObjects;
using Microsoft.Xna.Framework;
using MonoGameLibrary;
using MonoGameLibrary.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DungeonSlime.Menegers;

internal class EnemyMeneger
{
    private Player _player;
    public List<IEnemy> Enemies;
    private Sprite _commonEnemySprite;

    public EnemyMeneger(Player player)
    {
        _player = player;
    }
    public void Initialize()
    {
        Enemies = new List<IEnemy>();
        LoadContent();
    }

    public void LoadContent()
    {
        TextureAtlas atlas = TextureAtlas.FromFile(Core.Content, "Generated/atlas.xml");
        _commonEnemySprite = atlas.CreateSprite("HZ-08");
        _commonEnemySprite.CenterOrigin();
        _commonEnemySprite.Scale = Vector2.One * 0.25f;
        _commonEnemySprite.Color = Color.Green;

        Random rnd = new Random();

        for (int i = 0; i < 5; i++)
        {
            var slime = Create<CommonSlime>();
            slime.Activate();
            slime.Initialize(_player, _commonEnemySprite, new Vector2(
                50f + (float)rnd.NextDouble() * 1820f,  // 1870 - 50
                50f + (float)rnd.NextDouble() * 980f   // 1030 - 50
                ));
        }
    }

    public void Update()
    {
        foreach (IEnemy enemy in Enemies)
        {
            if (enemy.Active)
                ((GameObject)enemy).Update();
        }
    }
    public void Draw()
    {
        foreach (IEnemy enemy in Enemies)
        {
            if (enemy.Active)
                ((GameObject)enemy).Draw();
        }
    }

    public T Create<T>() where T : IEnemy, new()
    {
        // ищем неактивного
        foreach (var s in Enemies)
        {
            if (!s.Active && s is T reuse)
            {
                return reuse;
            }
        }

        // создаём нового
        T created = new T();
        created.Initialize();
        Enemies.Add(created);
        return created;
    }

    public void ClearAll()
    {
        Enemies.Clear();
    }
}
