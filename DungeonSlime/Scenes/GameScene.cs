using DungeonSlime.GameObjects;
using DungeonSlime.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;
using MonoGameLibrary;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Phisics;
using MonoGameLibrary.Scenes;
using RenderingLibrary;
using System;
using System.Diagnostics;

namespace DungeonSlime.Scenes;

public class GameScene : Scene
{
    private enum GameState
    {
        Playing,
        Paused,
        GameOver
    }

    // Reference to the slime.
    private Slime _slime;

    // Reference to the bat.
    private Bat _bat;

    private AnimatedSprite _exmp;

    // Defines the tilemap to draw.
    private Tilemap _tilemap;

    // Defines the bounds of the room that the slime and bat are contained within.
    private Rectangle _roomBounds;

    // The sound effect to play when the slime eats a bat.
    private SoundEffect _collectSoundEffect;

    // Tracks the players score.
    private int _score;

    private GameSceneUI _ui;

    private GameState _state;

    // The grayscale shader effect.

    // The amount of saturation to provide the grayscale shader effect.
    private float _saturation = 1.0f;

    // The speed of the fade to grayscale effect.
    private const float FADE_SPEED = 0.08f;

    Effect combinedEffect;

    Circle batBounds;

    private float _width = 1920;
    private float _height = 1080;
    //Test
    int boxColliderId;

    bool _isEnter;
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
        _slime.Pos = new Vector2(Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height) * 0.5f;

        // Initialize the slime.
        // Initialize the bat.
        _bat.RandomizeVelocity();
        PositionBatAwayFromSlime();

        // Reset the score.
        _score = 0;

        // Set the game state to playing.
        _state = GameState.Playing;
    }


    public override void LoadContent()
    {
        // Create the texture atlas from the XML configuration file.
        TextureAtlas atlas = TextureAtlas.FromFile(Core.Content, "images/atlas-definition.xml");
        // Create the tilemap from the XML configuration file.
        _tilemap = Tilemap.FromFile(Content, "images/tilemap-definition.xml");
        _tilemap.Scale = new Vector2((_width / 1280) * 4.0f, (_height / 720) * 4.0f);

        // Create the animated sprite for the slime from the atlas.
        AnimatedSprite slimeAnimation = atlas.CreateAnimatedSprite("slime-animation");
        slimeAnimation.Scale = new Vector2((_width / 1280) * 4.0f, (_height / 720) * 4.0f);

        // Create the slime.
        _slime = new Slime(slimeAnimation);
        _exmp = slimeAnimation;
        // Create the animated sprite for the bat from the atlas.
        AnimatedSprite batAnimation = atlas.CreateAnimatedSprite("bat-animation");
        batAnimation.Scale = new Vector2((_width / 1280) * 4.0f, (_height / 720) * 4.0f);

        // Load the bounce sound effect for the bat.
        SoundEffect bounceSoundEffect = Content.Load<SoundEffect>("audio/bounce");

        // Create the bat.
        _bat = new Bat(batAnimation, bounceSoundEffect);

        // Load the collect sound effect.
        _collectSoundEffect = Content.Load<SoundEffect>("audio/collect");

        // Load the grayscale effect.
        combinedEffect = Content.Load<Effect>("CombinedPost");

        _slime.Initialize();
        _bat.Initialize();
        Debug.WriteLine(Core.GraphicsDevice.Viewport.Width / 1280);
        Debug.WriteLine(Core.GraphicsDevice.Viewport.Height / 720);

        //Test
        // init collision system: capacity 256, layers = 5 (0..4)
        // register handlers
        Core.Cols.RegisterHandler(0, 2, (in CollisionSystem.CollisionInfo info) =>
        {
            Debug.WriteLine($"Collision: idA={info.IdA} (layer{info.LayerA}) hit idB={info.IdB} (layer{info.LayerB})");
            // Move the bat to a new position away from the slime.
            PositionBatAwayFromSlime();

            // Randomize the velocity of the bat.
            _bat.RandomizeVelocity();

            // Tell the slime to grow.

            // Increment the score.
            _score += 100;

            // Update the score display on the UI.
            _ui.UpdateScoreText(_score);

            // Play the collect sound effect.
            Core.Audio.PlaySoundEffect(_collectSoundEffect);
        });
        Core.Cols.RegisterHandler(0, 1, (in CollisionSystem.CollisionInfo info) =>
        {
            //Debug.WriteLine($"Collision: idA={info.IdA} (layer{info.LayerA}) hit idB={info.IdB} (layer{info.LayerB})");
            _isEnter = true;
        });
        boxColliderId = Core.Cols.CreateBox(new Vector2(Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height) * 0.5f, new Vector2(940, 520), layer: 1);

        // If you changed handlers after creation, no additional call needed because Register updates internal flags
    }


    public override void Update(GameTime gameTime)
    {
        // Ensure the UI is always updated.
        _ui.Update(gameTime);

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
        _slime.Update(gameTime);
        _bat.Update(gameTime);
        _bat.Check(_roomBounds);

        // box static; no update needed
        Core.Cols.ProcessCollisions();

        // At the end of update loop, process collisions
        if (!_isEnter)
        {
            GameOver();
            return;
        }
        _isEnter = false;
    }


    private void PositionBatAwayFromSlime()
    {
        // Calculate the position that is in the center of the bounds
        // of the room.
        float roomCenterX = _roomBounds.X + _roomBounds.Width * 0.5f;
        float roomCenterY = _roomBounds.Y + _roomBounds.Height * 0.5f;
        Vector2 roomCenter = new Vector2(roomCenterX, roomCenterY);

        // Get the bounds of the slime and calculate the center position.
        Circle slimeBounds = _slime.GetBounds();
        Vector2 slimeCenter = new Vector2(slimeBounds.X, slimeBounds.Y);

        // Calculate the distance vector from the center of the room to the
        // center of the slime.
        Vector2 centerToSlime = slimeCenter - roomCenter;

        // Get the bounds of the bat.
        batBounds = _bat.GetBounds();

        // Calculate the amount of padding we will add to the new position of
        // the bat to ensure it is not sticking to walls
        int padding = batBounds.Radius * 2;

        // Calculate the new position of the bat by finding which component of
        // the center to slime vector (X or Y) is larger and in which direction.
        Vector2 newBatPosition = Vector2.Zero;
        if (Math.Abs(centerToSlime.X) > Math.Abs(centerToSlime.Y))
        {
            // The slime is closer to either the left or right wall, so the Y
            // position will be a random position between the top and bottom
            // walls.
            newBatPosition.Y = Random.Shared.Next(
                _roomBounds.Top + padding,
                _roomBounds.Bottom - padding
            );

            if (centerToSlime.X > 0)
            {
                // The slime is closer to the right side wall, so place the
                // bat on the left side wall.
                newBatPosition.X = _roomBounds.Left + padding;
            }
            else
            {
                // The slime is closer ot the left side wall, so place the
                // bat on the right side wall.
                newBatPosition.X = _roomBounds.Right - padding * 2;
            }
        }
        else
        {
            // The slime is closer to either the top or bottom wall, so the X
            // position will be a random position between the left and right
            // walls.
            newBatPosition.X = Random.Shared.Next(
                _roomBounds.Left + padding,
                _roomBounds.Right - padding
            );

            if (centerToSlime.Y > 0)
            {
                // The slime is closer to the top wall, so place the bat on the
                // bottom wall.
                newBatPosition.Y = _roomBounds.Top + padding;
            }
            else
            {
                // The slime is closer to the bottom wall, so place the bat on
                // the top wall.
                newBatPosition.Y = _roomBounds.Bottom - padding * 2;
            }
        }

        // Assign the new bat position.
        _bat.Pos = newBatPosition;
    }

    private void OnSlimeBodyCollision(object sender, EventArgs args)
    {
        GameOver();
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
        //Circle[] colliders = new Circle[] { _bat.GetBounds(), _slime.GetBounds(), new Circle(200, 200, 100, new Color(100, 100, 100), 10) };
        Circle[] colliders = new Circle[] { Core.Cols.GetBounds(_slime.ColliderId), Core.Cols.GetBounds(_bat.ColliderId) };
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
        _tilemap.Draw(Core.SpriteBatch);
        _slime.Draw();
        _bat.Draw();
        _exmp.Draw(Core.SpriteBatch, new Vector2(600, 600));
        Core.SpriteBatch.End();

        // 2) Back to backbuffer
        Core.GraphicsDevice.SetRenderTarget(null);

        // 3) Set parameters for combined effect
        combinedEffect.Parameters["Texture0"].SetValue(Core.SceneTarget);
        combinedEffect.Parameters["ScreenSize"].SetValue(new Vector2(Core.GraphicsDevice.Viewport.Width, Core.GraphicsDevice.Viewport.Height));
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
