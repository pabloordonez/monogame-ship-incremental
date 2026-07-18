using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ShipGame.Content;
using ShipGame.Domain;
using ShipGame.Persistence;
using ShipGame.Simulation;

namespace ShipGame.Game;

public sealed class ShipGameHost : Microsoft.Xna.Framework.Game
{
    public const string CatalogFingerprint = "foundation-catalog-v1";
    private readonly GraphicsDeviceManager _graphics;
    private FoundationSimulation _simulation;
    private FixedStepDriver _driver;
    private readonly SaveRepository _saves;
    private readonly bool _windowSmoke;
    private ProfileSnapshot _profile;
    private KeyboardState _previousKeyboard;
    private AppState _previousState;
    private string _title = "SHIP GAME";
    private bool _windowSmokeVisitedSummary;

    public ShipGameHost(bool windowSmoke = false)
    {
        _windowSmoke = windowSmoke;
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        IsFixedTimeStep = false;
        Window.AllowUserResizing = true;

        _profile = new ProfileSnapshot(0x5348495047414D45UL, 0);
        _simulation = new FoundationSimulation(_profile.ProfileSeed, _profile.RunIndex);
        _driver = new FixedStepDriver(_simulation);
        _previousState = _simulation.State;
        var saveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShipGame");
        _saves = new SaveRepository(saveDirectory);
    }

    protected override void LoadContent()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Content");
        var manifest = ContentValidator.LoadAndValidateManifest(root, "data/asset-manifest.json");
        var catalog = new FileAssetCatalog(root, manifest);
        using var document = System.Text.Json.JsonDocument.Parse(
            catalog.LoadText(new ContentId("data/title-placeholder")));
        _title = document.RootElement.GetProperty("title").GetString() ?? _title;
        Window.Title = _title;
        if (_windowSmoke)
            Console.WriteLine("DESKTOPVK_CONTENT_READY");
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape))
            Exit();

        var enterPressed = keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter);
        var continuePressed = keyboard.IsKeyDown(Keys.C) && !_previousKeyboard.IsKeyDown(Keys.C);
        if (_windowSmoke && _simulation.State is AppState.Title or AppState.Lobby)
        {
            _simulation.Queue(new CommandFrame(_simulation.Tick, Confirm: true));
        }
        else if (_windowSmoke && _simulation.State == AppState.Summary)
        {
            _windowSmokeVisitedSummary = true;
            _simulation.Queue(new CommandFrame(_simulation.Tick, Return: true));
        }
        else if (continuePressed && _simulation.State == AppState.Title)
        {
            var loaded = _saves.Load(CatalogFingerprint);
            if (loaded.Status == CompatibilityStatus.Supported && loaded.Envelope is not null)
            {
                _profile = loaded.Envelope.Profile;
                ResetSimulation();
                _simulation.Queue(new CommandFrame(_simulation.Tick, Confirm: true));
            }
        }
        else if (enterPressed)
        {
            _simulation.Queue(new CommandFrame(_simulation.Tick, Confirm: true));
        }

        _driver.Advance(gameTime.ElapsedGameTime.TotalSeconds);
        PersistBoundaries();
        if (_windowSmoke && _windowSmokeVisitedSummary && _simulation.State == AppState.Lobby)
        {
            Console.WriteLine("DESKTOPVK_WALKING_SKELETON_COMPLETE");
            Exit();
        }
        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        var color = _simulation.State switch
        {
            AppState.Title => new Color(8, 12, 28),
            AppState.Lobby => new Color(12, 30, 40),
            AppState.Run => new Color(4, 8, 18),
            AppState.Summary => new Color(28, 20, 42),
            _ => Color.Black
        };
        GraphicsDevice.Clear(color);
        Window.Title = $"{_title} — {_simulation.State}";
        base.Draw(gameTime);
    }

    private void PersistBoundaries()
    {
        if (_simulation.State == _previousState)
            return;
        if (_simulation.State == AppState.Run)
            _profile = _profile with { RunIndex = _profile.RunIndex + 1 };
        if (_simulation.State is AppState.Run or AppState.Summary or AppState.Lobby)
            _saves.Write(_saves.CreateEnvelope(_profile, "P0_FOUNDATION", CatalogFingerprint));
        _previousState = _simulation.State;
    }

    private void ResetSimulation()
    {
        _simulation = new FoundationSimulation(_profile.ProfileSeed, _profile.RunIndex);
        _driver = new FixedStepDriver(_simulation);
        _previousState = _simulation.State;
    }
}

public static class SmokeRunner
{
    public static int Run(string? repositoryRoot = null, string? saveDirectory = null)
    {
        repositoryRoot ??= FindRepositoryRoot();
        var contentRoot = Path.Combine(repositoryRoot, "content", "generated", "DesktopVK", "Content");
        var manifest = ContentValidator.LoadAndValidateManifest(contentRoot, "data/asset-manifest.json");
        var catalog = new FileAssetCatalog(contentRoot, manifest);
        if (!catalog.LoadText(new ContentId("data/title-placeholder")).Contains("SHIP GAME", StringComparison.Ordinal))
            return 10;

        saveDirectory ??= Path.Combine(Path.GetTempPath(), "ShipGame-Smoke-" + Guid.NewGuid().ToString("N"));
        var saves = new SaveRepository(saveDirectory);
        var profile = new ProfileSnapshot(123456789UL, 0);
        var simulation = new FoundationSimulation(profile.ProfileSeed, profile.RunIndex);

        simulation.Queue(new CommandFrame(simulation.Tick, Confirm: true));
        simulation.Step();
        simulation.Queue(new CommandFrame(simulation.Tick, Confirm: true));
        simulation.Step();
        profile = profile with { RunIndex = profile.RunIndex + 1 };
        saves.Write(saves.CreateEnvelope(profile, "P0_FOUNDATION", ShipGameHost.CatalogFingerprint));

        while (simulation.State == AppState.Run)
            simulation.Step();
        if (simulation.State != AppState.Summary)
            return 11;
        simulation.Queue(new CommandFrame(simulation.Tick, Return: true));
        simulation.Step();
        var loaded = saves.Load(ShipGameHost.CatalogFingerprint);
        if (loaded.Status != CompatibilityStatus.Supported || loaded.Envelope?.Profile != profile)
            return 12;

        var continuedProfile = loaded.Envelope.Profile;
        var continued = new FoundationSimulation(continuedProfile.ProfileSeed, continuedProfile.RunIndex);
        continued.Queue(new CommandFrame(continued.Tick, Confirm: true));
        continued.Step();
        continued.Queue(new CommandFrame(continued.Tick, Confirm: true));
        continued.Step();
        var expectedRandom = new RandomStreams(
            FoundationSimulation.DeriveRunSeed(continuedProfile.ProfileSeed, continuedProfile.RunIndex));
        var expectedSignature = expectedRandom.Get(RngStream.Encounter).NextUInt();

        return simulation.State == AppState.Lobby &&
               continued.Seed == profile.ProfileSeed &&
               continued.RunIndex == profile.RunIndex &&
               continued.RunSignature == expectedSignature
            ? 0
            : 13;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ShipGame.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate ShipGame.sln.");
    }
}
