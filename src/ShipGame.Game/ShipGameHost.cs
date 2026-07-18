using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ShipGame.Content;
using ShipGame.Domain;
using ShipGame.Simulation;
using ShipGame.Telemetry;

namespace ShipGame.Game;

public sealed class ShipGameHost : Microsoft.Xna.Framework.Game
{
    public const string BuildId = "P5_INTEGRATION";
    private readonly GraphicsDeviceManager _graphics;
    private readonly bool _windowSmoke;
    private readonly string _saveDirectory;
    private MetaSession? _session;
    private ComposedRunOrchestrator? _run;
    private MvpPresentation? _presentation;
    private RuntimeContentCatalog? _catalog;
    private KeyboardState _previousKeyboard;
    private double _accumulator;
    private bool _windowSmokeVisitedSummary;
    private bool _windowSmokeContentVisible;
    private bool _windowSmokeHarnessStarted;
    private string _title = "SHIP GAME";
    private int _windowSmokeTicks;

    public ShipGameHost(bool windowSmoke = false, string? saveDirectory = null)
    {
        _windowSmoke = windowSmoke;
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        IsFixedTimeStep = false;
        Window.AllowUserResizing = true;
        Content.RootDirectory = "Content";
        _saveDirectory = saveDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShipGame");
    }

    protected override void LoadContent()
    {
        var repoRoot = FindRepositoryRoot();
        _catalog = MvpContentLoader.LoadAndValidate(
            Path.Combine(repoRoot, "content", "source"),
            Path.Combine(repoRoot, "content", "definitions"));
        _session = new MetaSession(
            _saveDirectory,
            () => JsonLinesTelemetrySink.Create(Path.Combine(_saveDirectory, "telemetry.jsonl")),
            newProfileSeed: MetaSession.DefaultNewProfileSeed);
        _presentation = new MvpPresentation(GraphicsDevice, _catalog);
        _presentation.LoadTextures(Content);

        var root = Path.Combine(AppContext.BaseDirectory, "Content");
        var manifest = ContentValidator.LoadAndValidateManifest(root, "data/asset-manifest.json");
        var textCatalog = new FileAssetCatalog(root, manifest);
        using var document = System.Text.Json.JsonDocument.Parse(
            textCatalog.LoadText(new ContentId("data/title-placeholder")));
        _title = document.RootElement.GetProperty("title").GetString() ?? _title;
        Window.Title = _title;
        if (_windowSmoke)
            Console.WriteLine("DESKTOPVK_CONTENT_READY");
    }

    protected override void Update(GameTime gameTime)
    {
        if (_session is null)
            return;

        var keyboard = Keyboard.GetState();
        HandleMetaInput(keyboard);
        if (_windowSmoke)
            DriveWindowSmoke();

        _accumulator += Math.Clamp(gameTime.ElapsedGameTime.TotalSeconds, 0, 0.25);
        const double tickSeconds = 1d / WorldRunSimulation.TickRate;
        var steps = 0;
        while (_accumulator >= tickSeconds && steps < 8)
        {
            StepSimulation();
            _accumulator -= tickSeconds;
            steps++;
        }

        if (_windowSmoke && _presentation?.DrewSpritesThisFrame == true)
            _windowSmokeContentVisible = true;

        if (_windowSmoke &&
            _windowSmokeVisitedSummary &&
            _session.Screen == MetaScreen.Lobby &&
            _windowSmokeContentVisible)
        {
            Console.WriteLine("DESKTOPVK_COMPOSED_LOOP_COMPLETE");
            Exit();
        }

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_session is null || _presentation is null)
        {
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);
            base.Draw(gameTime);
            return;
        }

        _presentation.DrawMetaScreen(
            _session.Screen,
            _session,
            _run,
            GraphicsDevice.Viewport.Width,
            GraphicsDevice.Viewport.Height);
        Window.Title = $"{_title} — {_session.Screen}";
        if (_windowSmoke && _presentation.DrewSpritesThisFrame)
            _windowSmokeContentVisible = true;
        base.Draw(gameTime);
    }

    private void StepSimulation()
    {
        if (_session is null || _session.Screen != MetaScreen.Run || _run is null)
            return;
        if (_run.Status == ComposedRunStatus.AwaitingUpgradeChoice)
            return;
        if (_run.Status == ComposedRunStatus.Terminal)
        {
            CommitRunReward();
            return;
        }

        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        var aim = MouseAimWorld(mouse);
        var command = FlightInputAdapters.Keyboard(
            _run.Combat.Tick,
            FlightInputAdapters.ReadKeyboard(keyboard, aim));
        var gamepad = GamePad.GetState(PlayerIndex.One);
        if (gamepad.IsConnected && gamepad.ThumbSticks.Left.Length() + gamepad.ThumbSticks.Right.Length() > 0.2f)
            command = FlightInputAdapters.Gamepad(_run.Combat.Tick, FlightInputAdapters.ReadGamepad(gamepad));
        _run.Step(command);
        if (_run.Status == ComposedRunStatus.Terminal)
            CommitRunReward();
    }

    private void HandleMetaInput(KeyboardState keyboard)
    {
        if (_session is null)
            return;
        var enter = Pressed(keyboard, Keys.Enter);
        var escape = Pressed(keyboard, Keys.Escape);
        var keyC = Pressed(keyboard, Keys.C);
        var keyM = Pressed(keyboard, Keys.M);
        var keyL = Pressed(keyboard, Keys.L);
        var keyR = Pressed(keyboard, Keys.R);
        var key1 = Pressed(keyboard, Keys.D1);
        var key2 = Pressed(keyboard, Keys.D2);
        var key3 = Pressed(keyboard, Keys.D3);

        if (escape)
        {
            if (_session.Screen == MetaScreen.Run)
            {
                _session.Navigate(MetaScreen.Pause);
                _run?.SetPaused(true);
            }
            else if (_session.Screen == MetaScreen.Pause)
            {
                _session.Back();
                _run?.SetPaused(false);
            }
            else if (_session.Screen is MetaScreen.Map or MetaScreen.Loadout or MetaScreen.Research or MetaScreen.Settings)
                _session.Back();
            else if (_session.Screen == MetaScreen.Title)
                Exit();
            return;
        }

        if (_session.Screen == MetaScreen.Run && _run?.Status == ComposedRunStatus.AwaitingUpgradeChoice)
        {
            if (key1) _run.ChooseUpgrade(0);
            if (key2) _run.ChooseUpgrade(1);
            if (key3) _run.ChooseUpgrade(2);
            return;
        }

        switch (_session.Screen)
        {
            case MetaScreen.Title when enter:
                if (_session.RequiresExplicitNewProfile)
                    _session.CreateNewProfile();
                _session.Navigate(MetaScreen.Lobby);
                break;
            case MetaScreen.Title when keyC:
                _session.ContinueFromDisk();
                if (_session.Screen == MetaScreen.Title && !_session.RequiresExplicitNewProfile)
                    _session.Navigate(MetaScreen.Lobby);
                break;
            case MetaScreen.Lobby when keyM:
                _session.Navigate(MetaScreen.Map);
                break;
            case MetaScreen.Lobby when keyL:
                _session.Navigate(MetaScreen.Loadout);
                break;
            case MetaScreen.Lobby when keyR:
                _session.Navigate(MetaScreen.Research);
                break;
            case MetaScreen.Lobby when enter:
                _session.Navigate(MetaScreen.Map);
                break;
            case MetaScreen.Map when enter:
                if (_session.Launch().Accepted)
                    StartRun();
                break;
            case MetaScreen.Summary when enter:
                _session.Navigate(MetaScreen.Lobby);
                _run = null;
                break;
        }
    }

    private void StartRun()
    {
        if (_session is null)
            return;
        var snapshot = _session.Profile.Snapshot;
        var stats = _session.Profile.DeriveStatistics();
        var loadout = _session.Profile.ResolveLoadout();
        var recovery = stats.FailureFerriteRetentionPercent >= 50;
        _run = new ComposedRunOrchestrator(
            new ContentId(_session.Ui.SelectedEnvironmentId),
            snapshot.ProfileSeed,
            snapshot.RunIndex,
            loadout,
            stats,
            recovery);
    }

    private void CommitRunReward()
    {
        if (_session is null || _run?.MappedReward is null)
            return;
        _session.CommitReward(_run.MappedReward);
        _run = null;
    }

    private void DriveWindowSmoke()
    {
        if (_session is null)
            return;
        _windowSmokeTicks++;
        switch (_session.Screen)
        {
            case MetaScreen.Title when _windowSmokeTicks > 30:
                _session.Navigate(MetaScreen.Lobby);
                break;
            case MetaScreen.Lobby when _windowSmokeTicks > 60:
                _session.Navigate(MetaScreen.Map);
                break;
            case MetaScreen.Map when _windowSmokeTicks > 90:
                if (_session.Launch().Accepted)
                    StartRun();
                break;
            case MetaScreen.Run when _run is not null && !_windowSmokeHarnessStarted && _windowSmokeTicks > 120:
                _windowSmokeHarnessStarted = true;
                var reward = _run.CompleteViaHarness(succeed: true);
                _session.CommitReward(reward);
                _windowSmokeVisitedSummary = true;
                _run = null;
                break;
            case MetaScreen.Summary when _windowSmokeTicks > 150:
                _session.Navigate(MetaScreen.Lobby);
                break;
        }
    }

    private System.Numerics.Vector2 MouseAimWorld(MouseState mouse)
    {
        if (_run is null || _run.Combat.Player == default)
            return System.Numerics.Vector2.UnitX;
        var player = _run.Combat.Snapshot(_run.Combat.Player).Position;
        var scale = Math.Max(1, Math.Min(
            GraphicsDevice.Viewport.Width / MvpPresentation.VirtualWidth,
            GraphicsDevice.Viewport.Height / MvpPresentation.VirtualHeight));
        var offsetX = (GraphicsDevice.Viewport.Width - MvpPresentation.VirtualWidth * scale) / 2;
        var offsetY = (GraphicsDevice.Viewport.Height - MvpPresentation.VirtualHeight * scale) / 2;
        var virtualX = (mouse.X - offsetX) / (float)scale;
        var virtualY = (mouse.Y - offsetY) / (float)scale;
        var world = new System.Numerics.Vector2(
            player.X + (virtualX - MvpPresentation.VirtualWidth / 2f),
            player.Y + (virtualY - MvpPresentation.VirtualHeight / 2f));
        var delta = world - player;
        return delta.LengthSquared() < 0.001f
            ? System.Numerics.Vector2.UnitX
            : System.Numerics.Vector2.Normalize(delta);
    }

    private bool Pressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    protected override void UnloadContent()
    {
        _presentation?.Dispose();
        _session?.Dispose();
        base.UnloadContent();
    }

    internal static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ShipGame.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate ShipGame.sln.");
    }
}

public static class SmokeRunner
{
    public static int Run(string? repositoryRoot = null, string? saveDirectory = null)
    {
        repositoryRoot ??= ShipGameHost.FindRepositoryRoot();
        var contentRoot = Path.Combine(repositoryRoot, "content", "generated", "DesktopVK", "Content");
        var manifest = ContentValidator.LoadAndValidateManifest(contentRoot, "data/asset-manifest.json");
        var catalog = new FileAssetCatalog(contentRoot, manifest);
        if (!catalog.LoadText(new ContentId("data/title-placeholder")).Contains("SHIP GAME", StringComparison.Ordinal))
            return 10;

        saveDirectory ??= Path.Combine(Path.GetTempPath(), "ShipGame-Smoke-" + Guid.NewGuid().ToString("N"));
        using var session = new MetaSession(saveDirectory, newProfileSeed: 123456789UL);
        if (session.Screen == MetaScreen.Title)
            session.Navigate(MetaScreen.Lobby);
        session.Navigate(MetaScreen.Map);
        if (!session.Launch().Accepted)
            return 11;

        var snapshot = session.Profile.Snapshot;
        var run = new ComposedRunOrchestrator(
            new ContentId(session.Ui.SelectedEnvironmentId),
            snapshot.ProfileSeed,
            snapshot.RunIndex,
            session.Profile.ResolveLoadout(),
            session.Profile.DeriveStatistics(),
            recoveryProtocols: false);
        var reward = run.CompleteViaHarness(succeed: true);
        if (!run.Checkpoints.Contains("extracted") || !run.Checkpoints.Contains("reward_mapped"))
            return 12;
        if (session.CommitReward(reward).Status != ProfileMutationStatus.Applied)
            return 13;
        if (session.Screen != MetaScreen.Summary)
            return 14;
        session.Navigate(MetaScreen.Lobby);

        using var continued = new MetaSession(saveDirectory);
        if (continued.Screen != MetaScreen.Lobby)
            return 15;
        continued.Navigate(MetaScreen.Map);
        if (!continued.Launch().Accepted)
            return 16;
        var continuedSnapshot = continued.Profile.Snapshot;
        var second = new ComposedRunOrchestrator(
            new ContentId(continued.Ui.SelectedEnvironmentId),
            continuedSnapshot.ProfileSeed,
            continuedSnapshot.RunIndex,
            continued.Profile.ResolveLoadout(),
            continued.Profile.DeriveStatistics(),
            recoveryProtocols: false);
        var secondReward = second.CompleteViaHarness(succeed: true);
        if (continued.CommitReward(secondReward).Status != ProfileMutationStatus.Applied)
            return 17;
        return continued.Screen == MetaScreen.Summary ? 0 : 18;
    }
}
