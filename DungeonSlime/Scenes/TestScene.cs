using DungeonSlime.GameObjects;
using DungeonSlime.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;
using MonoGameLibrary;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Input;
using MonoGameLibrary.Phisics;
using MonoGameLibrary.Scenes;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DungeonSlime.Scenes
{
    internal class TestScene : Scene
    {
        Sprite exampleSprite;
        RenderTarget2D sceneTarget;
        Effect combinedEffect;
        float _saturation;
        Vector2 _pos;
        int _boxId;
        int _circle1Id;
        int _playerId;
        int _circle2Id;
        public override void Initialize()
        {
            sceneTarget = new RenderTarget2D(
                Core.GraphicsDevice,
                Core.GraphicsDevice.Viewport.Width,
                Core.GraphicsDevice.Viewport.Height,
                false,
                Core.GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.None);
            base.Initialize();
            _saturation = 1.0f;
        }

        public override void LoadContent()
        {
            TextureAtlas atlas = TextureAtlas.FromFile(Core.Content, "Generated/atlas.xml");
            exampleSprite = atlas.CreateSprite("HZ-09");
            exampleSprite.CenterOrigin();
            exampleSprite.Scale = new Vector2(300, 300);
            exampleSprite.Color = Color.White;
            combinedEffect = Content.Load<Effect>("CombinedPost");
            Core.Cam.Position = Core.Viewport * 0.5f;
            _pos = Core.Viewport * 0.5f;
            _playerId = Core.Cols.CreateCircle(Core.Viewport * 0.5f, 80, layer: 0, Color.DarkSlateBlue);
            _boxId = Core.Cols.CreateBox(Core.Viewport * 0.33f, new Vector2(300, 300), layer: 1);
            _circle1Id = Core.Cols.CreateCircle(Vector2.Zero, 100, layer: 2, Color.White);
            _circle2Id = Core.Cols.CreateCircle(Core.Viewport * 0.55f, 100, layer: 3, Color.White);
            Core.Cols.RegisterEnterHandler(0, 1, (in CollisionSystem.CollisionInfo info) => EnterBox());
            Core.Cols.RegisterEnterHandler(0, 2, (in CollisionSystem.CollisionInfo info) => EnterCircle());
            Core.Cols.RegisterExitHandler(0, 1, (in CollisionSystem.CollisionInfo info) => ExitBox());
            Core.Cols.RegisterExitHandler(0, 2, (in CollisionSystem.CollisionInfo info) => ExitCircle());

            Core.Cols.RegisterEnterHandler(2, 3, (in CollisionSystem.CollisionInfo info) => Core.Cols.SetColor(_circle2Id, Color.DeepPink));
            Core.Cols.RegisterExitHandler(2, 3, (in CollisionSystem.CollisionInfo info) => Core.Cols.SetColor(_circle2Id, Color.White));
            Core.Cols.RegisterEnterHandler(1, 2, (in CollisionSystem.CollisionInfo info) => exampleSprite.Color = Color.DeepPink);
            Core.Cols.RegisterExitHandler(1, 2, (in CollisionSystem.CollisionInfo info) => exampleSprite.Color = Color.White);
        }

        public override void Update(GameTime gameTime)
        {
            Debug.WriteLine(gameTime.TotalGameTime.ToString());
            Core.Cols.SetPosition(_playerId, _pos);
            Core.Cols.SetPosition(_circle1Id, Core.Viewport * 0.5f + Vector2.UnitX * (float)Math.Cos(gameTime.TotalGameTime.TotalSeconds * 2) * 200 + Vector2.UnitY * (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 2) * 200);
            Core.Cols.SetPosition(_circle2Id, Core.Viewport * 0.5f + Vector2.UnitX * (float)Math.Cos(gameTime.TotalGameTime.TotalSeconds * 4) * 150 + Vector2.UnitY * (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4) * 150);
            Vector2 _vel = Vector2.Zero;
            _vel += Vector2.UnitY * -Convert.ToInt32(GameController.MoveUp()) +
            Vector2.UnitY * Convert.ToInt32(GameController.MoveDown()) +
            Vector2.UnitX * -Convert.ToInt32(GameController.MoveLeft()) +
            Vector2.UnitX * Convert.ToInt32(GameController.MoveRight());
            _vel -= (_vel * Convert.ToInt32(_vel.X != 0 && _vel.Y != 0) * 0.27f);
            _pos += _vel * 8;

            Core.Cols.ProcessCollisions();
        }

        public override void Draw(GameTime gameTime)
        {
            List<Circle> colliders = new List<Circle> {Core.Cols.GetBounds(_playerId), Core.Cols.GetBounds(_circle1Id), Core.Cols.GetBounds(_circle2Id) };
            int count = Math.Min(colliders.Count, 64);
            Vector4[] data = new Vector4[count];    // CircleData packed
            Vector4[] cols = new Vector4[count];    // CircleColor

            for (int i = 0; i < count; i++)
            {
                var c = colliders[i];
                var t = Core.Cam.WorldToScreen(new Vector2(c.X, c.Y));
                data[i] = new Vector4(t.X, t.Y, c.Radius, c.OutlineThickness);
                cols[i] = new Vector4(c.Color.R / 255f, c.Color.G / 255f, c.Color.B / 255f, c.Color.A / 255f);
            }
            for (int i = count; i < count; i++)
            {
                data[i] = Vector4.Zero;
                cols[i] = Vector4.Zero;
            }

            // 1) Render scene into sceneTarget
            Core.GraphicsDevice.SetRenderTarget(Core.SceneTarget);
            Core.GraphicsDevice.Clear(Color.Gray);
            Core.SpriteBatch.Begin(SpriteSortMode.Deferred, samplerState: SamplerState.PointClamp, transformMatrix: Core.Cam.GetMatrix());
            exampleSprite.Draw(Core.SpriteBatch, Core.Viewport * 0.33f);
            Core.SpriteBatch.End();

            Core.GraphicsDevice.SetRenderTarget(null);

            combinedEffect.Parameters["Texture0"].SetValue(sceneTarget);
            combinedEffect.Parameters["ScreenSize"].SetValue(new Vector2(Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height));
            combinedEffect.Parameters["ShowCollision"].SetValue(true);
            combinedEffect.Parameters["Texture0"].SetValue(Core.SceneTarget);
            combinedEffect.Parameters["ScreenSize"].SetValue(new Vector2(Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height));
            combinedEffect.Parameters["Saturation"].SetValue(1 - _saturation);
            combinedEffect.Parameters["CircleCount"].SetValue(count);
            combinedEffect.Parameters["CircleData"].SetValue(data);
            combinedEffect.Parameters["CircleColor"].SetValue(cols);
            combinedEffect.Parameters["ShowCollision"].SetValue(true);

            Core.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, combinedEffect);
            Core.SpriteBatch.Draw(Core.SceneTarget, new Rectangle(0, 0, Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height), Color.White);
            Core.SpriteBatch.End();
        }

        void ExitBox()
        {
            exampleSprite.Color = Color.White;
        }

        void ExitCircle()
        {
            Core.Cols.SetColor(_circle1Id, Color.White);
        }

        void EnterBox()
        {
            exampleSprite.Color = Color.Red;
        }

        void EnterCircle()
        {
            Core.Cols.SetColor(_circle1Id, Color.Red);
        }
    }
}