using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ShipGame.Content;
using ShipGame.Domain;
using ShipGame.Gameplay;
using ShipGame.Telemetry;

namespace ShipGame.Game;

public sealed class ShipGameHost : Microsoft.Xna.Framework.Game
{
    public const string BuildId = "P5_INTEGRATION";
    private readonly GraphicsDeviceManager _graphics;
    private readonly bool _windowSmoke;
    private readonly string _saveDirectory;
    private readonly UiShell _ui = new();
    private MetaScreenHandlerRegistry? _screenHandlers;
    private MetaUiContext? _uiContext;
    private MetaSession? _session;
    private ComposedRunOrchestrator? _run;
    private MvpPresentation? _presentation;
    private RuntimeContentCatalog? _catalog;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private RunPresentationHints _hints;
    private double _accumulator;
    private bool _windowSmokeContentVisible;
    private string _title = "SHIP GAME";
    private int _windowSmokeTicks;
    private int _transactionSerial;
    private string? _lastFocusedId;

    public ShipGameHost(bool windowSmoke = false, string? saveDirectory = null)
    {
        _windowSmoke = windowSmoke;
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        IsFixedTimeStep = false;
        IsMouseVisible = false;
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
        _screenHandlers = MetaScreenHandlerRegistry.CreateDefault();
        _presentation = new MvpPresentation(GraphicsDevice, _catalog, _screenHandlers);
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
        var mouse = Mouse.GetState();
        var screenBefore = _session.Screen;
        var runStatusBefore = _run?.Status;
        RebuildUi();
        HandleUiInput(keyboard, mouse);
        if (_session.Screen != screenBefore || _run?.Status != runStatusBefore)
            RebuildUi();
        RefreshHints(keyboard, mouse);
        if (_windowSmoke)
            DriveWindowSmoke();

        _accumulator += Math.Clamp(gameTime.ElapsedGameTime.TotalSeconds, 0, 0.25);
        const double tickSeconds = 1d / WorldRun.TickRate;
        var steps = 0;
        while (_accumulator >= tickSeconds && steps < 8)
        {
            StepSimulation();
            _accumulator -= tickSeconds;
            steps++;
        }

        if (_windowSmoke &&
            _presentation?.DrewAtlasRegionThisFrame == true &&
            _presentation.TexturesLoaded > 0)
            _windowSmokeContentVisible = true;

        var uiContext = EnsureUiContext();
        if (_windowSmoke &&
            uiContext.WindowSmokeVisitedSummary &&
            _session.Screen == MetaScreen.Station &&
            _windowSmokeContentVisible)
        {
            Console.WriteLine("DESKTOPVK_COMPOSED_LOOP_COMPLETE");
            Exit();
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
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
            _ui,
            _hints,
            GraphicsDevice.Viewport.Width,
            GraphicsDevice.Viewport.Height);
        Window.Title = $"{_title} — {_session.Screen}";
        if (_windowSmoke &&
            _presentation.DrewAtlasRegionThisFrame &&
            _presentation.TexturesLoaded > 0)
            _windowSmokeContentVisible = true;
        base.Draw(gameTime);
    }

    private void RebuildUi()
    {
        if (_session is null)
            return;

        var screen = EffectiveUiScreen();
        var context = EnsureUiContext();
        context.Run = _run;
        _ui.Begin(screen);
        EnsureScreenHandlers().BuildUi(screen, context);
        _ui.EndBuild();
        SyncMapSelectionFromFocus();
    }

    private MetaScreenHandlerRegistry EnsureScreenHandlers() =>
        _screenHandlers ??= MetaScreenHandlerRegistry.CreateDefault();

    private MetaUiContext EnsureUiContext() =>
        _uiContext ??= new MetaUiContext
        {
            Ui = _ui,
            Session = _session!,
            Run = _run,
            ExitGame = Exit,
            StartRun = StartRun,
            ClearRun = () => _run = null,
            CommitRunReward = CommitRunReward,
            NextTransactionId = NextTx,
            ApplySettings = ApplySettings,
            EffectiveModule = EffectiveModule
        };

    private MetaScreen EffectiveUiScreen() => _session?.Screen ?? MetaScreen.Title;

    private void ApplySettings(GameSettings settings)
    {
        _session?.ApplySettings(NextTx("settings"), settings);
    }

    private string EffectiveModule(ModuleSlot slot)
    {
        var loadout = _session!.Profile.ResolveLoadout().Effective;
        return slot switch
        {
            ModuleSlot.Weapon => loadout.Weapon,
            ModuleSlot.Mining => loadout.Mining,
            ModuleSlot.Shield => loadout.Shield,
            ModuleSlot.Engine => loadout.Engine,
            ModuleSlot.Utility => loadout.Utility,
            _ => ""
        };
    }

    private void SyncMapSelectionFromFocus()
    {
        if (_session?.Screen != MetaScreen.Map)
        {
            _lastFocusedId = _ui.FocusedId;
            return;
        }

        var focused = _ui.FocusedId;
        if (focused is null || string.Equals(focused, _lastFocusedId, StringComparison.Ordinal))
        {
            _lastFocusedId = focused;
            return;
        }

        _lastFocusedId = focused;
        if (!focused.StartsWith("env:", StringComparison.Ordinal))
            return;
        var envId = focused["env:".Length..];
        var view = _session.Map.FirstOrDefault(env =>
            string.Equals(env.EnvironmentId, envId, StringComparison.Ordinal));
        if (view is { Accessible: true })
            _session.SelectEnvironment(envId);
    }

    private void HandleUiInput(KeyboardState keyboard, MouseState mouse)
    {
        if (_session is null)
            return;

        var escape = Pressed(keyboard, Keys.Escape);
        if (escape)
        {
            HandleEscape();
            return;
        }

        var uiActive = _ui.Controls.Count > 0;
        if (uiActive)
        {
            if (Pressed(keyboard, Keys.Up) || Pressed(keyboard, Keys.W))
            {
                _ui.MoveFocus(-1);
                SyncMapSelectionFromFocus();
            }

            if (Pressed(keyboard, Keys.Down) || Pressed(keyboard, Keys.S))
            {
                _ui.MoveFocus(1);
                SyncMapSelectionFromFocus();
            }

            if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space))
                _ui.TryActivateFocused();

            var context = EnsureUiContext();
            context.Run = _run;
            EnsureScreenHandlers().HandleHotkeys(_session.Screen, context, key => Pressed(keyboard, key));

            if (UiShell.TryMapScreenToVirtual(
                    mouse.X,
                    mouse.Y,
                    GraphicsDevice.Viewport.Width,
                    GraphicsDevice.Viewport.Height,
                    out var vx,
                    out var vy))
            {
                var leftDown = mouse.LeftButton == ButtonState.Pressed;
                var leftPressed = leftDown && _previousMouse.LeftButton != ButtonState.Pressed;
                // During flight, do not steal LMB for UI.
                if (_session.Screen != MetaScreen.Run)
                {
                    _ui.UpdatePointer(vx, vy, leftDown, leftPressed);
                    SyncMapSelectionFromFocus();
                }
            }
        }
    }

    private void HandleEscape()
    {
        if (_session is null)
            return;
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
        else if (_session.Screen is MetaScreen.Map or MetaScreen.Loadout or MetaScreen.Research or
                 MetaScreen.Upgrades or MetaScreen.Settings)
            _session.Back();
        else if (_session.Screen == MetaScreen.Title)
            Exit();
    }

    private void RefreshHints(KeyboardState keyboard, MouseState mouse)
    {
        var stats = _session?.Profile.DeriveStatistics();
        var maxHull = stats?.MaximumHull ?? 100;
        var maxShield = stats?.ShieldCapacity ?? 50;
        var flashes = _session?.Profile.Snapshot.Settings.Flashes ?? true;
        var move = System.Numerics.Vector2.Zero;
        var aim = System.Numerics.Vector2.UnitX;
        var mouseVirtual = System.Numerics.Vector2.Zero;
        var showCursor = false;
        var showReticle = false;
        var fire = false;
        var mine = false;

        if (UiShell.TryMapScreenToVirtual(
                mouse.X,
                mouse.Y,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                out var vx,
                out var vy))
        {
            mouseVirtual = new System.Numerics.Vector2(vx, vy);
            showCursor = true;
        }

        if (_session?.Screen == MetaScreen.Run && _run is not null)
        {
            move = new System.Numerics.Vector2(
                (keyboard.IsKeyDown(Keys.D) ? 1 : 0) - (keyboard.IsKeyDown(Keys.A) ? 1 : 0),
                (keyboard.IsKeyDown(Keys.S) ? 1 : 0) - (keyboard.IsKeyDown(Keys.W) ? 1 : 0));
            if (move.LengthSquared() > 1f)
                move = System.Numerics.Vector2.Normalize(move);
            aim = MouseAimWorld(mouse);
            fire = mouse.LeftButton == ButtonState.Pressed;
            mine = mouse.RightButton == ButtonState.Pressed && !fire;
            showReticle = showCursor;
        }

        _hints = new RunPresentationHints(
            move,
            aim,
            mouseVirtual,
            showCursor,
            showReticle,
            flashes,
            maxHull,
            maxShield,
            fire,
            mine);
    }

    private void StepSimulation()
    {
        if (_session is null || _session.Screen != MetaScreen.Run || _run is null)
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
            recovery,
            purchasedUpgradeIds: snapshot.PurchasedUpgradeIds);
        if (_uiContext is not null)
            _uiContext.Run = _run;
    }

    private void CommitRunReward()
    {
        if (_session is null || _run?.MappedReward is null)
            return;
        _session.CommitReward(_run.MappedReward);
        _run = null;
        if (_uiContext is not null)
            _uiContext.Run = null;
    }

    private void DriveWindowSmoke()
    {
        if (_session is null)
            return;
        _windowSmokeTicks++;
        var context = EnsureUiContext();
        context.Run = _run;
        EnsureScreenHandlers().DriveWindowSmoke(_session.Screen, context, _windowSmokeTicks);
        _run = context.Run;
    }

    private System.Numerics.Vector2 MouseAimWorld(MouseState mouse)
    {
        if (_run is null || _run.Combat.Player == default)
            return System.Numerics.Vector2.UnitX;
        var player = _run.Combat.Snapshot(_run.Combat.Player).Position;
        if (!UiShell.TryMapScreenToVirtual(
                mouse.X,
                mouse.Y,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                out var virtualX,
                out var virtualY))
            return System.Numerics.Vector2.UnitX;
        var world = new System.Numerics.Vector2(
            player.X + (virtualX - MvpPresentation.VirtualWidth / 2f),
            player.Y + (virtualY - MvpPresentation.VirtualHeight / 2f));
        var delta = world - player;
        return delta.LengthSquared() < 0.001f
            ? System.Numerics.Vector2.UnitX
            : System.Numerics.Vector2.Normalize(delta);
    }

    private string NextTx(string prefix) => $"TX_UI_{prefix}_{++_transactionSerial}";

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
