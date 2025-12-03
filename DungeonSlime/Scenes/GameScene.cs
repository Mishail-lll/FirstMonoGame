using DungeonSlime.GameObjects;
using DungeonSlime.UI;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;
using MonoGameGum.Forms.DefaultVisuals;
using MonoGameLibrary;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Phisics;
using MonoGameLibrary.Scenes;
using MonoGameLibrary.Input;
using RenderingLibrary;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using static MonoGameLibrary.Phisics.CollisionSystem;
using DungeonSlime.Menegers;

namespace DungeonSlime.Scenes;

public class GameScene : Scene
{
    private enum GameState
    {
        Playing,
        Paused,
        GameOver
    }

    private Player _player;

    // Defines the tilemap to draw.
    private Tilemap _tilemap;

    // Defines the bounds of the room that the slime and bat are contained within.
    private Rectangle _roomBounds;


    // Tracks the players score.

    private GameSceneUI _ui;

    private GameState _state;

    private EnemyMeneger _enemyMeneger;

    // The grayscale shader effect.

    // The amount of saturation to provide the grayscale shader effect.
    private float _saturation = 1.0f;

    // The speed of the fade to grayscale effect.
    private const float FADE_SPEED = 0.08f;

    Effect combinedEffect;


    private float _width = 1920;
    private float _height = 1080;
    //Test
    int boxColliderId;
    static public readonly Queue<Action> _queue = new Queue<Action>();

    void OnEnterEnemy()
    {
        Core.Audio.PlaySoundEffectByKey("collect");
    }
    void exitCollisionAction()
    {
        Core.Audio.PlaySoundEffectByKey("collect");
        GameOver();
    }
    public override void Initialize()
    {
        // LoadContent is called during base.Initialize().
        base.Initialize();

        // During the game scene, we want to disable exit on escape. Instead,
        // the escape key will be used to return back to the title screen.
        Core.ExitOnEscape = false;

        // Create the room bounds by getting the bounds of the screen then
        // using the Inflate method to "Deflate" the bounds by the width and
        // height of a tile so that the bounds only covers the inside room of
        // the dungeon tilemap.
        _roomBounds = Core.GraphicsDevice.PresentationParameters.Bounds;
        _roomBounds.Inflate(-_tilemap.TileWidth, -_tilemap.TileHeight);

        // Subscribe to the slime's BodyCollision event so that a game over
        // can be triggered when this event is raised.

        // Create any UI elements from the root element created in previous
        // scenes.
        GumService.Default.Root.Children.Clear();

        // Initialize the user interface for the game scene.
        InitializeUI();

        // Initialize a new game to be played.
        InitializeNewGame();


        Core.Cols.DebugDumpState();
    }

    private void InitializeUI()
    {
        // Clear out any previous UI element incase we came here
        // from a different scene.
        GumService.Default.Root.Children.Clear();

        // Create the game scene ui instance.
        _ui = new GameSceneUI();

        // Subscribe to the events from the game scene ui.
        _ui.ResumeButtonClick += OnResumeButtonClicked;
        _ui.RetryButtonClick += OnRetryButtonClicked;
        _ui.QuitButtonClick += OnQuitButtonClicked;
        _ui.UpdateScoreText(_player.Hp);
    }

    private void OnResumeButtonClicked(object sender, EventArgs args)
    {
        // Change the game state back to playing.
        _state = GameState.Playing;
    }

    private void OnRetryButtonClicked(object sender, EventArgs args)
    {
        // Player has chosen to retry, so initialize a new game.
        InitializeNewGame();
    }

    private void OnQuitButtonClicked(object sender, EventArgs args)
    {
        // Player has chosen to quit, so return back to the title scene.
        Core.ChangeScene(new TitleScene());
    }

    private void InitializeNewGame()
    {
        // Calculate the position for the slime, which will be at the center
        // tile of the tile map.
        _player.Pos = Core.Viewport * 0.5f;
        Core.Cols.SetPosition(_player.ColliderId, new Vector2(_player.Pos.X + _player.Sprite.Width * 0.5f, _player.Pos.Y + _player.Sprite.Height * 0.5f));
        // Initialize the slime.
        // Initialize the bat.
        // Reset the score.

        // Set the game state to playing.
        _state = GameState.Playing;
    }


    public override void LoadContent()
    {
        // Create the texture atlasLegecy from the XML configuration file.
        TextureAtlas atlasLegecy = TextureAtlas.FromFile(Core.Content, "images/atlas-definition.xml");

        // Create new atlas

        // Create the tilemap from the XML configuration file.
        _tilemap = Tilemap.FromFile(Content, "images/tilemap-definition.xml");
        _tilemap.Scale = new Vector2((_width / 1280) * 4.0f, (_height / 720) * 4.0f);

        // Create the animated sprite for the slime from the atlasLegecy.
        AnimatedSprite spriteAnimation = atlasLegecy.CreateAnimatedSprite("slime-animation");
        spriteAnimation.Scale = new Vector2((_width / 1280) * 4.0f, (_height / 720) * 4.0f);
        spriteAnimation.CenterOrigin();

        // Create the slime.
        _player = new Player(spriteAnimation);
        _enemyMeneger = new EnemyMeneger(_player);

        // Create the animated sprite for the bat from the atlasLegecy.
        AnimatedSprite batAnimation = atlasLegecy.CreateAnimatedSprite("bat-animation");
        batAnimation.Scale = new Vector2((_width / 1280) * 4.0f, (_height / 720) * 4.0f);

        // Load the bounce sound effect for the bat.
        SoundEffect bounceSoundEffect = Content.Load<SoundEffect>("audio/bounce");
        // Load the collect sound effect.
        Core.Audio.Load("collect", "audio/collect");

        // Load the grayscale effect.
        combinedEffect = Content.Load<Effect>("CombinedPost");

        _player.Initialize();
        _enemyMeneger.Initialize();


        //Test
        // init collision system: capacity 256, layers = 5 (0..4)
        // register handlers
        Core.Cols.RegisterExitHandler(0, 1, (in CollisionSystem.CollisionInfo info) => exitCollisionAction());
        Core.Cols.RegisterEnterHandler(0, 3, (in CollisionSystem.CollisionInfo info) => exitCollisionAction());
        Core.Cols.RegisterEnterHandler(0, 2, (in CollisionSystem.CollisionInfo info) => OnEnterEnemy());
        boxColliderId = Core.Cols.CreateBox(Core.Viewport * 0.5f, new Vector2(940, 520), layer: 1);

        // If you changed handlers after creation, no additional call needed because Register updates internal flags
    }


    public override void Update(GameTime gameTime)
    {
        // Ensure the UI is always updated.
        _ui.Update(gameTime);
        _ui.UpdateScoreText(_player.Hp);
        if (_state != GameState.Playing)
        {
            // The game is in either a paused or game over state, so
            // gradually decrease the saturation to create the fading grayscale.
            _saturation = Math.Max(0.0f, _saturation - FADE_SPEED);

            // If its just a game over state, return back.
            if (_state == GameState.GameOver)
            {
                return;
            }
        }
        else
        {
            _saturation = 1.0f;
        }

        // If the pause button is pressed, toggle the pause state.
        if (GameController.JustPause())
        {
            TogglePause();
        }

        // At this point, if the game is paused, just return back early.
        if (_state == GameState.Paused)
        {
            return;
        }


        // Update the slime.
        _player.Update(gameTime);
        _enemyMeneger.Update();
        Core.Cols.ProcessCollisions();
        if (_player.Hp <= 0)
        {
            GameOver();
        }
    }

    private void TogglePause()
    {
        if (_state == GameState.Paused)
        {
            // We're now unpausing the game, so hide the pause panel.
            _ui.HidePausePanel();

            // And set the state back to playing.
            _state = GameState.Playing;
        }
        else
        {
            // We're now pausing the game, so show the pause panel.
            _ui.ShowPausePanel();

            // And set the state to paused.
            _state = GameState.Paused;

            // Set the grayscale effect saturation to 1.0f
            _saturation = 1.0f;
        }

    }

    private void GameOver()
    {
        // Show the game over panel.
        _ui.ShowGameOverPanel();
        // Set the game state to game over.
        _state = GameState.GameOver;

        // Set the grayscale effect saturation to 1.0f
        _saturation = 1.0f;
    }


    public override void Draw(GameTime gameTime)
    {
        //Circle[] colliders = new Circle[] { _bat.GetBounds(), _player.GetBounds(), new Circle(200, 200, 100, new Color(100, 100, 100), 10) };
        List<Circle> colliders = new List<Circle> { Core.Cols.GetBounds(_player.ColliderId) };
        if (Core.Cols.ValidId(_enemyMeneger.Enemies[0].GetId()))
        {
            colliders.Add( Core.Cols.GetBounds(_enemyMeneger.Enemies[0].GetId()));
        }
        int count = Math.Min(colliders.Count, 48);
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
        _tilemap.Draw(Core.SpriteBatch);
        _player.Draw();
        _enemyMeneger.Draw();
        Core.SpriteBatch.End();

        // 2) Back to backbuffer
        Core.GraphicsDevice.SetRenderTarget(null);

        // 3) Set parameters for combined effect
        combinedEffect.Parameters["Texture0"].SetValue(Core.SceneTarget);
        combinedEffect.Parameters["ScreenSize"].SetValue(Core.Viewport);
        combinedEffect.Parameters["Saturation"].SetValue(1 - _saturation);

        combinedEffect.Parameters["CircleCount"].SetValue(count);
        combinedEffect.Parameters["CircleData"].SetValue(data);
        combinedEffect.Parameters["CircleColor"].SetValue(cols);
        combinedEffect.Parameters["ShowCollision"].SetValue(true);

        // 3) Drow the effects
        Core.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, combinedEffect);
        Core.SpriteBatch.Draw(Core.SceneTarget, new Rectangle(0, 0, Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height), Color.White);
        Core.SpriteBatch.End();

        // 5) Draw UI on top
        _ui.Draw();
    }



}
