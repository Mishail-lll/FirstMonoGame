using System;
using System.Diagnostics;
using DungeonSlime.GameObjects;
using DungeonSlime.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;
using MonoGameLibrary;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Scenes;

namespace DungeonSlime.Scenes
{
    internal class TestScene : Scene
    {
        Sprite exampleSprite;
        RenderTarget2D sceneTarget;
        Effect combinedEffect;
        float _saturation;
        Vector2 _pos = new Vector2(960, 540);
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
            exampleSprite = atlas.CreateSprite("HZ-02");
            exampleSprite.CenterOrigin();
            exampleSprite.Scale = new Vector2(1, 1);
            combinedEffect = Content.Load<Effect>("CombinedPost");
        }

        public override void Update(GameTime gameTime)
        {
            Core.Cam.Position = _pos;
        }

        public override void Draw(GameTime gameTime)
        {
            Circle[] colliders = new Circle[] { new Circle(0, 0, 50, new Color(255, 10, 10, 170), 10), new Circle(180, 180, 80, new Color(155, 155, 155, 200), 40) };
            int count = Math.Min(colliders.Length, 48);
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
            Core.GraphicsDevice.Clear(Color.CornflowerBlue);

            Core.SpriteBatch.Begin(SpriteSortMode.Deferred, samplerState: SamplerState.PointClamp, transformMatrix: Core.Cam.GetMatrix());
            exampleSprite.Draw(Core.SpriteBatch, _pos);
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

    }
}