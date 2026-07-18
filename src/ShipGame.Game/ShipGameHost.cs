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
    private readonly UiShell _ui = new();
    private MetaSession? _session;
    private ComposedRunOrchestrator? _run;
    private MvpPresentation? _presentation;
    private RuntimeContentCatalog? _catalog;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private RunPresentationHints _hints;
    private double _accumulator;
    private bool _windowSmokeVisitedSummary;
    private bool _windowSmokeContentVisible;
    private bool _windowSmokeHarnessStarted;
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
        const double tickSeconds = 1d / WorldRunSimulation.TickRate;
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

        if (_windowSmoke &&
            _windowSmokeVisitedSummary &&
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
        _ui.Begin(screen);
        switch (screen)
        {
            case MetaScreen.Title:
                BuildTitleUi();
                break;
            case MetaScreen.Station:
                BuildStationUi();
                break;
            case MetaScreen.Map:
                BuildMapUi();
                break;
            case MetaScreen.Loadout:
                BuildLoadoutUi();
                break;
            case MetaScreen.Research:
                BuildResearchUi();
                break;
            case MetaScreen.Upgrades:
                BuildStationUpgradesUi();
                break;
            case MetaScreen.Summary:
                BuildSummaryUi();
                break;
            case MetaScreen.Settings:
                BuildSettingsUi();
                break;
            case MetaScreen.Pause:
                BuildPauseUi();
                break;
        }

        _ui.EndBuild();
        SyncMapSelectionFromFocus();
    }

    private MetaScreen EffectiveUiScreen() => _session?.Screen ?? MetaScreen.Title;

    private void BuildTitleUi()
    {
        if (_session is null)
            return;
        _ui.Add("title:new", new UiRect(200, 180, 240, 32), "Enter  New Game / Station", true, () =>
        {
            if (_session.RequiresExplicitNewProfile)
                _session.CreateNewProfile();
            _session.Navigate(MetaScreen.Station);
        });
        _ui.Add(
            "title:continue",
            new UiRect(200, 220, 240, 32),
            "C  Continue Save",
            !_session.RequiresExplicitNewProfile,
            () =>
            {
                _session.ContinueFromDisk();
                if (_session.Screen == MetaScreen.Title && !_session.RequiresExplicitNewProfile)
                    _session.Navigate(MetaScreen.Station);
            });
        _ui.Add("title:quit", new UiRect(200, 260, 240, 32), "Esc  Quit", true, Exit);
    }

    private void BuildStationUi()
    {
        if (_session is null)
            return;
        _ui.Add("station:map", new UiRect(24, 170, 280, 32), "M  Environment Map", true, () => _session.Navigate(MetaScreen.Map));
        _ui.Add("station:loadout", new UiRect(24, 210, 280, 32), "L  Loadout", true, () => _session.Navigate(MetaScreen.Loadout));
        _ui.Add("station:research", new UiRect(24, 250, 280, 32), "R  Research", true, () => _session.Navigate(MetaScreen.Research));
        _ui.Add("station:upgrades", new UiRect(24, 290, 280, 32), "U  Upgrades", true, () => _session.Navigate(MetaScreen.Upgrades));
        _ui.Add("station:settings", new UiRect(320, 170, 280, 32), "O  Settings", true, () => _session.Navigate(MetaScreen.Settings));
    }

    private void BuildMapUi()
    {
        if (_session is null)
            return;
        var y = 56;
        foreach (var env in _session.Map)
        {
            var envId = env.EnvironmentId;
            var label = MvpPresentation.ShortId(envId);
            var prefix = env.Selected ? "> " : "  ";
            _ui.Add(
                $"env:{envId}",
                new UiRect(24, y, 592, 48),
                $"{prefix}{label}",
                true,
                () =>
                {
                    if (env.Accessible)
                        _session.SelectEnvironment(envId);
                });
            y += 56;
        }

        _ui.Add("map:launch", new UiRect(24, 280, 220, 32), "Enter  Launch", true, () =>
        {
            if (_session.Launch().Accepted)
                StartRun();
        });
        _ui.Add("map:back", new UiRect(260, 280, 160, 32), "Esc  Back", true, () => _session.Back());
    }

    private void BuildLoadoutUi()
    {
        if (_session is null)
            return;
        var y = 48;
        foreach (var slot in new[]
                 {
                     ModuleSlot.Weapon, ModuleSlot.Mining, ModuleSlot.Shield, ModuleSlot.Engine, ModuleSlot.Utility
                 })
        {
            foreach (var preview in _session.Ui.BuildLoadoutView(slot))
            {
                if (y > 300)
                    break;
                var equipped = string.Equals(
                    EffectiveModule(slot),
                    preview.ModuleId,
                    StringComparison.Ordinal);
                var ready = preview.Unlocked && preview.Compatible && preview.Known;
                var label =
                    $"{(equipped ? "*" : " ")} {slot} {MvpPresentation.ShortId(preview.ModuleId)}" +
                    (ready ? "" : " [locked]");
                var moduleId = preview.ModuleId;
                var moduleSlot = slot;
                _ui.Add(
                    $"loadout:{slot}:{moduleId}",
                    new UiRect(24, y, 480, 22),
                    label,
                    ready && !equipped,
                    () => _session.EquipModule(NextTx("equip"), moduleSlot, moduleId));
                y += 24;
            }
        }

        _ui.Add("loadout:back", new UiRect(24, 320, 160, 28), "Esc  Back", true, () => _session.Back());
    }

    private void BuildResearchUi()
    {
        if (_session is null)
            return;
        var y = 48;
        foreach (var node in _session.Research.Take(10))
        {
            var ready = !node.Purchased && node.Affordable && node.PrerequisitesMet && node.GateMet;
            var cost = $"{node.Definition.Cost.Ferrite}F/{node.Definition.Cost.Lumen}L/{node.Definition.Cost.DataCores}C";
            var status = node.Purchased ? "OWNED" : ready ? $"READY {cost}" : node.Affordable ? "LOCKED" : $"NEED {cost}";
            var researchId = node.Definition.Id;
            _ui.Add(
                $"research:{researchId}",
                new UiRect(24, y, 592, 22),
                $"{MvpPresentation.ShortId(researchId)}  {status}",
                ready,
                () => _session.PurchaseResearch(NextTx("research"), researchId));
            y += 24;
        }

        _ui.Add("research:back", new UiRect(24, 320, 160, 28), "Esc  Back", true, () => _session.Back());
    }

    private void BuildStationUpgradesUi()
    {
        if (_session is null)
            return;
        var y = 48;
        foreach (var node in _session.Upgrades.Take(10))
        {
            var ready = !node.Purchased && node.Affordable;
            var cost = $"{node.Definition.Cost.Ferrite}F/{node.Definition.Cost.Lumen}L/{node.Definition.Cost.DataCores}C";
            var status = node.Purchased ? "OWNED" : ready ? $"READY {cost}" : $"NEED {cost}";
            var upgradeId = node.Definition.Id.Value;
            _ui.Add(
                $"upg:{upgradeId}",
                new UiRect(24, y, 592, 22),
                $"{MvpPresentation.ShortId(upgradeId)}  {status}",
                ready,
                () => _session.PurchaseUpgrade(NextTx("upgrade"), upgradeId));
            y += 24;
        }

        _ui.Add("upgrades:back", new UiRect(24, 320, 160, 28), "Esc  Back", true, () => _session.Back());
    }

    private void BuildSummaryUi()
    {
        if (_session is null)
            return;
        _ui.Add("summary:station", new UiRect(24, 280, 280, 32), "Enter  Return to Station", true, () =>
        {
            _session.Navigate(MetaScreen.Station);
            _run = null;
        });
    }

    private void BuildSettingsUi()
    {
        if (_session is null)
            return;
        var settings = _session.Profile.Snapshot.Settings;
        _ui.Add(
            "settings:shake",
            new UiRect(24, 70, 400, 28),
            $"Screen Shake  {(settings.ScreenShake ? "ON" : "OFF")}",
            true,
            () => ApplySettings(settings with { ScreenShake = !settings.ScreenShake }));
        _ui.Add(
            "settings:flashes",
            new UiRect(24, 106, 400, 28),
            $"Flashes  {(settings.Flashes ? "ON" : "OFF")}",
            true,
            () => ApplySettings(settings with { Flashes = !settings.Flashes }));
        _ui.Add(
            "settings:vibration",
            new UiRect(24, 142, 400, 28),
            $"Vibration  {(settings.Vibration ? "ON" : "OFF")}",
            true,
            () => ApplySettings(settings with { Vibration = !settings.Vibration }));
        _ui.Add(
            "settings:telemetry",
            new UiRect(24, 178, 400, 28),
            $"Telemetry Consent  {(settings.TelemetryConsent ? "ON" : "OFF")}",
            true,
            () => ApplySettings(settings with { TelemetryConsent = !settings.TelemetryConsent }));
        _ui.Add(
            "settings:master",
            new UiRect(24, 214, 400, 28),
            $"Master Volume  {settings.MasterVolume}",
            true,
            () =>
            {
                var next = settings.MasterVolume <= 0 ? 100 : Math.Max(0, settings.MasterVolume - 20);
                ApplySettings(settings with { MasterVolume = next });
            });
        _ui.Add("settings:back", new UiRect(24, 280, 160, 28), "Esc  Back", true, () => _session.Back());
    }

    private void BuildPauseUi()
    {
        if (_session is null)
            return;
        _ui.Add("pause:resume", new UiRect(200, 160, 240, 32), "Esc  Resume", true, () =>
        {
            _session.Back();
            _run?.SetPaused(false);
        });
        _ui.Add("pause:settings", new UiRect(200, 204, 240, 32), "O  Settings", true, () => _session.Navigate(MetaScreen.Settings));
    }

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

            HandleHotkeys(keyboard);

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

    private void HandleHotkeys(KeyboardState keyboard)
    {
        if (_session is null)
            return;
        switch (_session.Screen)
        {
            case MetaScreen.Title:
                if (Pressed(keyboard, Keys.C))
                    _ui.Focus("title:continue");
                if (Pressed(keyboard, Keys.C))
                    _ui.TryActivateFocused();
                break;
            case MetaScreen.Station:
                if (Pressed(keyboard, Keys.M))
                {
                    _ui.Focus("station:map");
                    _ui.TryActivateFocused();
                }
                else if (Pressed(keyboard, Keys.L))
                {
                    _ui.Focus("station:loadout");
                    _ui.TryActivateFocused();
                }
                else if (Pressed(keyboard, Keys.R))
                {
                    _ui.Focus("station:research");
                    _ui.TryActivateFocused();
                }
                else if (Pressed(keyboard, Keys.U))
                {
                    _ui.Focus("station:upgrades");
                    _ui.TryActivateFocused();
                }
                else if (Pressed(keyboard, Keys.O))
                {
                    _ui.Focus("station:settings");
                    _ui.TryActivateFocused();
                }

                break;
            case MetaScreen.Pause:
                if (Pressed(keyboard, Keys.O))
                {
                    _ui.Focus("pause:settings");
                    _ui.TryActivateFocused();
                }

                break;
        }
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
                _session.Navigate(MetaScreen.Station);
                break;
            case MetaScreen.Station when _windowSmokeTicks > 60:
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
                _session.Navigate(MetaScreen.Station);
                break;
        }
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
            session.Navigate(MetaScreen.Station);
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
            recoveryProtocols: false,
            purchasedUpgradeIds: snapshot.PurchasedUpgradeIds);
        var reward = run.CompleteViaHarness(succeed: true);
        if (!run.Checkpoints.Contains("extracted") || !run.Checkpoints.Contains("reward_mapped"))
            return 12;
        if (session.CommitReward(reward).Status != ProfileMutationStatus.Applied)
            return 13;
        if (session.Screen != MetaScreen.Summary)
            return 14;
        session.Navigate(MetaScreen.Station);

        using var continued = new MetaSession(saveDirectory);
        if (continued.Screen != MetaScreen.Station)
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
            recoveryProtocols: false,
            purchasedUpgradeIds: continuedSnapshot.PurchasedUpgradeIds);
        var secondReward = second.CompleteViaHarness(succeed: true);
        if (continued.CommitReward(secondReward).Status != ProfileMutationStatus.Applied)
            return 17;
        return continued.Screen == MetaScreen.Summary ? 0 : 18;
    }
}
