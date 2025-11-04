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

            // Пример текстуры
            exampleSprite = Content.Load<Texture2D>("images/background-pattern");


            collisionEffect = Content.Load<Effect>("CollisionHighlight");
        }

        public override void Update(GameTime gameTime)
        {

        }

        public override void Draw(GameTime gameTime)
        {
            // --- 1) Рендерим сцену в сцена-таргет ---
            Core.GraphicsDevice.SetRenderTarget(sceneTarget);
            Core.GraphicsDevice.Clear(Color.CornflowerBlue);

            Core.SpriteBatch.Begin(); // рендер обычной сцены без effect
            Core.SpriteBatch.Draw(exampleSprite, new Vector2(100, 100), Color.White);        // твоя отрисовка тайлмапа, спрайтов и т.д.
            Core.SpriteBatch.End();

            // --- 2) Возвращаем рендер на экран ---
            Core.GraphicsDevice.SetRenderTarget(null);

            // --- 3) Передаём параметры шейдеру ---
            collisionEffect.Parameters["Texture0"].SetValue(sceneTarget); // важное: texture вход
            collisionEffect.Parameters["ScreenSize"].SetValue(new Vector2(Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height));

            // примеры: центр и радиус в экранных пикселях
            Vector2 circleCenter = new Vector2(400f, 300f); // если используешь камеру — преобразуй в экран coords
            float circleRadius = 100f;

            collisionEffect.Parameters["CircleCenter"].SetValue(circleCenter);
            collisionEffect.Parameters["CircleRadius"].SetValue(circleRadius);
            collisionEffect.Parameters["HighlightColor"].SetValue(new Vector4(0f, 1f, 0f, 0.6f)); // зелёный, a=0.6
            collisionEffect.Parameters["ShowCollision"].SetValue(true);

            // --- 4) Рисуем fullscreen quad с effect: он читает sceneTarget и накладывает подсветку ---
            Core.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, collisionEffect);
            Core.SpriteBatch.Draw(sceneTarget, new Rectangle(0, 0, Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height), Color.White);
            Core.SpriteBatch.End();

            // --- 5) UI поверх (если нужно) ---
        }

    }
}