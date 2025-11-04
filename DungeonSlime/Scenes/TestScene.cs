using System;
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
        Texture2D exampleSprite;
        RenderTarget2D sceneTarget;
        Effect collisionEffect;
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
        }

        public override void LoadContent()
        {
            exampleSprite = Content.Load<Texture2D>("images/background-pattern");


            collisionEffect = Content.Load<Effect>("CollisionHighlight");
        }

        public override void Update(GameTime gameTime)
        {

        }

        public override void Draw(GameTime gameTime)
        {
            Core.GraphicsDevice.SetRenderTarget(sceneTarget);
            Core.GraphicsDevice.Clear(Color.CornflowerBlue);

            Core.SpriteBatch.Begin();
            Core.SpriteBatch.Draw(exampleSprite, new Vector2(100, 100), Color.White);
            Core.SpriteBatch.End();

            Core.GraphicsDevice.SetRenderTarget(null);

            collisionEffect.Parameters["Texture0"].SetValue(sceneTarget);
            collisionEffect.Parameters["ScreenSize"].SetValue(new Vector2(Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height));

            Vector2 circleCenter = new Vector2(400f, 300f);
            float circleRadius = 100f;

            collisionEffect.Parameters["CircleCenter"].SetValue(circleCenter);
            collisionEffect.Parameters["CircleRadius"].SetValue(circleRadius);
            collisionEffect.Parameters["HighlightColor"].SetValue(new Vector4(0f, 1f, 0f, 0.6f));
            collisionEffect.Parameters["ShowCollision"].SetValue(true);

            Core.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, collisionEffect);
            Core.SpriteBatch.Draw(sceneTarget, new Rectangle(0, 0, Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height), Color.White);
            Core.SpriteBatch.End();
        }

    }
}