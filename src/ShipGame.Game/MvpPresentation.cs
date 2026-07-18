using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShipGame.Content;
using ShipGame.Domain;
using ShipGame.Simulation;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace ShipGame.Game;

/// <summary>
/// 640×360 integer-scaled presentation bound to catalog atlas regions (P5).
/// </summary>
public sealed class MvpPresentation : IDisposable
{
    public const int VirtualWidth = 640;
    public const int VirtualHeight = 360;

    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _spriteBatch;
    private readonly RuntimeContentCatalog _catalog;
    private readonly PixelFont _font;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly Texture2D _pixel;
    private readonly CombatPresentationBinding _combatCues = new();
    private bool _drewSprites;
    private bool _drewAtlasRegion;
    private int _texturesLoaded;
    private float _flashAlpha;
    private XnaColor _flashColor = XnaColor.White;
    private long _lastFlashTick = -1;

    public MvpPresentation(GraphicsDevice device, RuntimeContentCatalog catalog)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _spriteBatch = new SpriteBatch(device);
        _font = new PixelFont(device);
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData([XnaColor.White]);
    }

    public bool DrewSpritesThisFrame => _drewSprites;
    public bool DrewAtlasRegionThisFrame => _drewAtlasRegion;
    public int TexturesLoaded => _texturesLoaded;

    public void LoadTextures(Microsoft.Xna.Framework.Content.ContentManager content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _texturesLoaded = 0;
        foreach (var assetId in _catalog.AssetIds)
        {
            var asset = _catalog.GetAsset(assetId);
            if (asset.Kind is not ("texture" or "atlas"))
                continue;
            try
            {
                _textures[assetId] = content.Load<Texture2D>(assetId);
                _texturesLoaded++;
            }
            catch (Exception)
            {
                // Missing optional texture stays absent; UI falls back to solid rectangles.
            }
        }
    }

    public void BeginFrame()
    {
        _drewSprites = false;
        _drewAtlasRegion = false;
    }

    public void DrawMetaScreen(
        MetaScreen screen,
        MetaSession session,
        ComposedRunOrchestrator? run,
        UiShell ui,
        RunPresentationHints hints,
        int backBufferWidth,
        int backBufferHeight)
    {
        BeginFrame();
        var scale = Math.Max(1, Math.Min(backBufferWidth / VirtualWidth, backBufferHeight / VirtualHeight));
        var offsetX = (backBufferWidth - VirtualWidth * scale) / 2;
        var offsetY = (backBufferHeight - VirtualHeight * scale) / 2;

        _device.Clear(new XnaColor(4, 6, 12));
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: Matrix.CreateScale(scale) *
                             Matrix.CreateTranslation(offsetX, offsetY, 0));

        Fill(0, 0, VirtualWidth, VirtualHeight, ScreenBackground(screen));
        switch (screen)
        {
            case MetaScreen.Title:
                DrawTitle(ui);
                break;
            case MetaScreen.Station:
                DrawStation(session, ui);
                break;
            case MetaScreen.Map:
                DrawMap(session, ui);
                break;
            case MetaScreen.Loadout:
                DrawLoadout(session, ui);
                break;
            case MetaScreen.Research:
                DrawResearch(session, ui);
                break;
            case MetaScreen.Upgrades:
                DrawUpgrades(session, ui);
                break;
            case MetaScreen.Summary:
                DrawSummary(session, ui);
                break;
            case MetaScreen.Settings:
                DrawSettings(session, ui);
                break;
            case MetaScreen.Pause:
                DrawPause(ui);
                break;
            case MetaScreen.Run:
                DrawRun(run, ui, hints, session);
                break;
        }

        Frame(0, 0, VirtualWidth, VirtualHeight, new XnaColor(180, 200, 220), 2);
        if (hints.ShowCursor)
            DrawMouseCursor(hints.MouseVirtual, hints.FireHeld || ui.PressedId is not null);
        _spriteBatch.End();
    }

    private void DrawTitle(UiShell ui)
    {
        DrawRegion("ui/icons/objective", 296, 48, 48, 48);
        DrawText(220, 110, "SHIP GAME", new XnaColor(240, 245, 255), 2);
        DrawText(200, 150, "Mouse or keyboard to choose", new XnaColor(160, 180, 200));
        DrawShellButtons(ui);
        DrawRegion("ui/icons/interact", 304, 300, 32, 32);
    }

    private void DrawStation(MetaSession session, UiShell ui)
    {
        var station = session.Station;
        DrawText(24, 16, "STATION", new XnaColor(230, 240, 255), 2);
        DrawText(24, 36, "Banked materials from the field", new XnaColor(160, 180, 200));
        DrawRegion("ships/player/wayfarer", 520, 40, 72, 72);
        DrawRegion("ui/icons/resource-ferrite", 24, 56, 16, 16);
        DrawText(46, 58, $"Ferrite {station.Balances.Ferrite}", new XnaColor(220, 200, 160));
        DrawRegion("ui/icons/resource-lumen", 24, 78, 16, 16);
        DrawText(46, 80, $"Lumen {station.Balances.Lumen}", new XnaColor(180, 220, 255));
        DrawRegion("ui/icons/resource-data-core", 24, 100, 16, 16);
        DrawText(46, 102, $"Cores {station.Balances.DataCores}", new XnaColor(200, 180, 255));
        if (station.PreviousRun is { } previous)
            DrawText(
                24,
                128,
                previous.Succeeded ? "Last run: EXTRACTED" : "Last run: FAILED",
                previous.Succeeded ? new XnaColor(140, 220, 160) : new XnaColor(220, 140, 140));

        DrawText(24, 148, "Spend banked resources between flights", new XnaColor(180, 200, 220));
        DrawShellButtons(ui);
        DrawText(24, 340, "Arrows focus  Enter/click activate", new XnaColor(140, 150, 160));
    }

    private void DrawUpgrades(MetaSession session, UiShell ui)
    {
        DrawText(24, 16, "UPGRADES", new XnaColor(230, 240, 255), 2);
        DrawText(24, 36, "Permanent perks — paid with banked Ferrite/Lumen/Cores", new XnaColor(180, 200, 220));
        DrawBankedPurse(session);
        DrawShellButtons(ui);
        DrawText(24, 340, "Enter/click purchase when READY  Esc station", new XnaColor(140, 150, 160));
    }

    private void DrawBankedPurse(MetaSession session)
    {
        var balances = session.Station.Balances;
        DrawRegion("ui/icons/resource-ferrite", 400, 16, 14, 14);
        DrawText(418, 18, $"{balances.Ferrite}", new XnaColor(220, 200, 160));
        DrawRegion("ui/icons/resource-lumen", 470, 16, 14, 14);
        DrawText(488, 18, $"{balances.Lumen}", new XnaColor(180, 220, 255));
        DrawRegion("ui/icons/resource-data-core", 530, 16, 14, 14);
        DrawText(548, 18, $"{balances.DataCores}", new XnaColor(200, 180, 255));
    }

    private void DrawMap(MetaSession session, UiShell ui)
    {
        DrawText(24, 16, "SELECT ENVIRONMENT", new XnaColor(230, 240, 255), 2);
        foreach (var env in session.Map)
        {
            var id = $"env:{env.EnvironmentId}";
            var control = FindControl(ui, id);
            if (control is null)
                continue;
            var state = ui.GetState(id);
            DrawButton(control.Bounds, control.Label, state);
            if (!env.Accessible)
            {
                DrawRegion("ui/icons/lock", control.Bounds.X + control.Bounds.Width - 36, control.Bounds.Y + 10, 20, 20);
                DrawText(
                    control.Bounds.X + 12,
                    control.Bounds.Y + 28,
                    Truncate(env.Explanation, 64),
                    new XnaColor(160, 160, 170));
            }
            else
                DrawText(
                    control.Bounds.X + 12,
                    control.Bounds.Y + 28,
                    env.Selected ? "Selected — Launch when ready" : "Select then Launch",
                    new XnaColor(180, 190, 200));
        }

        DrawShellButtons(ui, skipPrefix: "env:");
        DrawText(24, 340, "Up/Down select  Enter/click Launch  Esc station", new XnaColor(140, 150, 160));
    }

    private void DrawLoadout(MetaSession session, UiShell ui)
    {
        DrawText(24, 16, "LOADOUT", new XnaColor(230, 240, 255), 2);
        DrawText(24, 36, "Equip modules unlocked via Research (no extra cost)", new XnaColor(180, 200, 220));
        DrawBankedPurse(session);
        DrawRegion("ships/player/wayfarer", 520, 48, 72, 72);
        DrawShellButtons(ui);
        DrawText(24, 340, "Enter/click equip unlocked module  Esc station", new XnaColor(140, 150, 160));
    }

    private void DrawResearch(MetaSession session, UiShell ui)
    {
        DrawText(24, 16, "RESEARCH", new XnaColor(230, 240, 255), 2);
        DrawText(24, 36, "Spend banked Ferrite/Lumen/Cores — unlocks loadout modules", new XnaColor(180, 200, 220));
        DrawBankedPurse(session);
        DrawShellButtons(ui);
        DrawText(24, 340, "Enter/click purchase when READY  Esc station", new XnaColor(140, 150, 160));
    }

    private void DrawSummary(MetaSession session, UiShell ui)
    {
        var previous = session.Profile.Snapshot.PreviousRun;
        DrawText(24, 16, "RUN SUMMARY", new XnaColor(230, 240, 255), 2);
        if (previous is null)
        {
            DrawText(24, 64, "No previous run.", XnaColor.White);
        }
        else
        {
            DrawText(
                24,
                56,
                previous.Succeeded ? "RESULT  EXTRACTED" : "RESULT  FAILED",
                previous.Succeeded ? new XnaColor(140, 220, 160) : new XnaColor(220, 140, 140),
                2);
            DrawText(24, 88, $"Banked Ferrite {previous.Banked.Ferrite}", XnaColor.White);
            DrawText(24, 104, $"Banked Lumen   {previous.Banked.Lumen}", XnaColor.White);
            DrawText(24, 120, $"Banked Cores   {previous.Banked.DataCores}", XnaColor.White);
            DrawText(24, 144, $"Lost Ferrite   {previous.Lost.Ferrite}", new XnaColor(200, 160, 160));
        }

        DrawRegion("ui/icons/objective", 520, 56, 48, 48);
        DrawShellButtons(ui);
    }

    private void DrawSettings(MetaSession session, UiShell ui)
    {
        DrawText(24, 16, "SETTINGS", new XnaColor(230, 240, 255), 2);
        DrawText(24, 44, "Toggle accessibility and audio options.", new XnaColor(180, 200, 220));
        DrawShellButtons(ui);
        DrawText(24, 340, "Enter/click toggle  Esc back", new XnaColor(140, 150, 160));
        _ = session;
    }

    private void DrawPause(UiShell ui)
    {
        Fill(0, 0, VirtualWidth, VirtualHeight, new XnaColor(0, 0, 0, 140));
        DrawRegion("ui/icons/pause", 304, 48, 32, 32);
        DrawText(260, 90, "PAUSED", new XnaColor(230, 240, 255), 2);
        DrawText(200, 120, "Simulation clock stopped.", new XnaColor(180, 200, 220));
        DrawShellButtons(ui);
    }

    private void DrawRun(
        ComposedRunOrchestrator? run,
        UiShell ui,
        RunPresentationHints hints,
        MetaSession session)
    {
        if (run is null)
        {
            DrawPanel("RUN", "Composing encounter...", "Please wait");
            return;
        }

        var bgId = run.EnvironmentId.Value == MetaContentIds.IonVeil
            ? "backgrounds/ion-veil"
            : "backgrounds/cinder-belt";
        var hud = run.Hud;
        var camera = run.Combat.Player != default
            ? run.Combat.Snapshot(run.Combat.Player).Position
            : System.Numerics.Vector2.Zero;

        DrawParallaxBackground(bgId, camera);
        UpdateCombatFlash(run, hints);

        foreach (var asteroid in run.Asteroids)
        {
            if (asteroid.Broken)
                continue;
            var screen = WorldToScreen(new System.Numerics.Vector2(asteroid.X, asteroid.Y), camera);
            if (!OnScreen(screen, 32))
                continue;
            var region = asteroid.Kind switch
            {
                AsteroidCellKind.Ferrite => "asteroids/medium/ferrite",
                AsteroidCellKind.Lumen => "asteroids/medium/lumen",
                _ => "asteroids/medium/ordinary"
            };
            DrawRegion(region, (int)screen.X - 12, (int)screen.Y - 12, 24, 24);
        }

        foreach (var pickup in run.Pickups)
        {
            var screen = WorldToScreen(new System.Numerics.Vector2(pickup.X, pickup.Y), camera);
            if (!OnScreen(screen, 16))
                continue;
            var region = pickup.ResourceId.Value switch
            {
                MetaContentIds.Lumen => "pickups/lumen",
                MetaContentIds.DataCore => "pickups/data-core",
                _ => "pickups/ferrite"
            };
            DrawRegion(region, (int)screen.X - 6, (int)screen.Y - 6, 12, 12);
        }

        var playerScreen = new XnaVector2(VirtualWidth / 2f, VirtualHeight / 2f);
        foreach (var item in run.LiveRenderItems)
        {
            var screen = WorldToScreen(item.Position, camera);
            if (!OnScreen(screen, 40))
                continue;
            switch (item.Kind)
            {
                case CombatRenderKind.PlayerShip:
                    playerScreen = screen;
                    DrawThrustTrail(screen, hints.MoveIntent, hud.RunTick);
                    DrawRegionRotated("ships/player/wayfarer", screen, item.Rotation, 32);
                    if (hints.FireHeld)
                        DrawMuzzleFlash(screen, hints.AimDirection, hud.RunTick);
                    if (hints.MineHeld)
                        DrawMineRay(screen, hints.AimDirection);
                    break;
                case CombatRenderKind.EnemyShip:
                    DrawRegionRotated(
                        item.Elite ? "enemies/elite-outline" : "enemies/interceptor",
                        screen,
                        item.Rotation,
                        item.Elite ? 28 : 22);
                    break;
                case CombatRenderKind.Projectile:
                    DrawRegion("projectiles/hostile", (int)screen.X - 3, (int)screen.Y - 3, 6, 6);
                    break;
                case CombatRenderKind.Mine:
                    DrawRegion("telegraphs/mine-radius", (int)screen.X - 10, (int)screen.Y - 10, 20, 20);
                    break;
            }
        }

        var extract = WorldToScreen(
            new System.Numerics.Vector2(
                run.Descriptor.Extraction.Center.X * FieldDescriptor.WorldUnitsPerCell,
                run.Descriptor.Extraction.Center.Y * FieldDescriptor.WorldUnitsPerCell),
            camera);
        if (OnScreen(extract, 40))
            DrawRegion("field/extraction-marker", (int)extract.X - 16, (int)extract.Y - 16, 32, 32);

        if (hints.ShowAimReticle)
            DrawAimReticle(hints.MouseVirtual);

        if (_flashAlpha > 0.01f && hints.FlashesEnabled)
        {
            Fill(0, 0, VirtualWidth, VirtualHeight, _flashColor * _flashAlpha);
            _flashAlpha = MathF.Max(0f, _flashAlpha - 0.08f);
        }

        DrawRunHud(hud, hints, playerScreen);

        if (hud.Phase == RunPhase.Extraction)
            DrawText(
                8,
                52,
                $"Hold E in extract zone: {hud.ExtractionProgressTicks}/{hud.ExtractionHoldTicks}",
                new XnaColor(160, 220, 180));

        _ = ui;
        _ = session;
    }

    private void DrawRunHud(ComposedRunHud hud, RunPresentationHints hints, XnaVector2 playerScreen)
    {
        Fill(0, 0, VirtualWidth, 48, new XnaColor(0, 0, 0, 180));
        DrawRegion("ui/icons/hull", 8, 6, 16, 16);
        DrawBar(28, 8, 90, 10, hints.MaxHull <= 0 ? 0 : hud.Hull / hints.MaxHull, new XnaColor(200, 90, 90));
        DrawText(122, 6, $"{hud.Hull:0}", XnaColor.White);

        DrawRegion("ui/icons/shield", 160, 6, 16, 16);
        DrawBar(180, 8, 90, 10, hints.MaxShield <= 0 ? 0 : hud.Shield / Math.Max(1f, hints.MaxShield), new XnaColor(80, 180, 220));
        DrawText(274, 6, $"{hud.Shield:0}", XnaColor.White);

        DrawRegion("ui/icons/resource-ferrite", 320, 6, 16, 16);
        DrawText(340, 6, $"{hud.FerriteHeld}", new XnaColor(220, 200, 160));
        DrawText(400, 6, $"Obj {hud.ObjectiveFerrite}/30  K {hud.ObjectiveKills}/8", XnaColor.White);

        DrawText(
            8,
            28,
            $"{hud.Phase}  t{hud.RunTick}  WASD move  mouse aim  LMB fire  RMB mine  Space dash  E extract",
            new XnaColor(180, 200, 220));

        // Move-direction tick near the ship so strafe is visible while camera-locked.
        if (hints.MoveIntent.LengthSquared() > 0.01f)
        {
            var dir = System.Numerics.Vector2.Normalize(hints.MoveIntent);
            var tip = new XnaVector2(playerScreen.X + dir.X * 22f, playerScreen.Y + dir.Y * 22f);
            Fill((int)tip.X - 2, (int)tip.Y - 2, 4, 4, new XnaColor(120, 220, 160));
        }
    }

    private void DrawShellButtons(UiShell ui, string? skipPrefix = null)
    {
        foreach (var control in ui.Controls)
        {
            if (skipPrefix is not null && control.Id.StartsWith(skipPrefix, StringComparison.Ordinal))
                continue;
            if (control.Id.StartsWith("env:", StringComparison.Ordinal))
                continue;
            DrawButton(control.Bounds, control.Label, ui.GetState(control.Id));
        }
    }

    private void DrawButton(UiRect bounds, string label, UiControlState state)
    {
        var fill = state switch
        {
            UiControlState.Disabled => new XnaColor(28, 32, 40, 220),
            UiControlState.Pressed => new XnaColor(70, 110, 140, 240),
            UiControlState.Focused => new XnaColor(40, 70, 96, 230),
            UiControlState.Hovered => new XnaColor(36, 58, 78, 230),
            _ => new XnaColor(20, 28, 40, 220)
        };
        var frame = state switch
        {
            UiControlState.Disabled => new XnaColor(80, 85, 95),
            UiControlState.Pressed => new XnaColor(255, 240, 180),
            UiControlState.Focused => new XnaColor(240, 250, 255),
            UiControlState.Hovered => new XnaColor(200, 220, 240),
            _ => new XnaColor(120, 140, 160)
        };
        var text = state switch
        {
            UiControlState.Disabled => new XnaColor(120, 125, 135),
            UiControlState.Pressed => new XnaColor(255, 255, 240),
            _ => XnaColor.White
        };

        var inset = state == UiControlState.Pressed ? 2 : 0;
        Fill(bounds.X + inset, bounds.Y + inset, bounds.Width - inset * 2, bounds.Height - inset * 2, fill);
        Frame(bounds.X, bounds.Y, bounds.Width, bounds.Height, frame, state is UiControlState.Focused or UiControlState.Pressed ? 2 : 1);
        if (state is UiControlState.Focused or UiControlState.Hovered)
            Frame(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4, frame * 0.65f, 1);

        DrawText(bounds.X + 12 + inset, bounds.Y + Math.Max(6, (bounds.Height - 8) / 2) + inset, label, text);
    }

    private void DrawParallaxBackground(string bgId, System.Numerics.Vector2 camera)
    {
        const float parallax = 0.18f;
        var ox = (int)MathF.Floor(-camera.X * parallax);
        var oy = (int)MathF.Floor(-camera.Y * parallax);
        ox %= VirtualWidth;
        oy %= VirtualHeight;
        if (ox > 0) ox -= VirtualWidth;
        if (oy > 0) oy -= VirtualHeight;
        for (var y = oy; y < VirtualHeight; y += VirtualHeight)
        {
            for (var x = ox; x < VirtualWidth; x += VirtualWidth)
                DrawTexture(bgId, x, y, VirtualWidth, VirtualHeight, XnaColor.White * 0.9f);
        }
    }

    private void DrawThrustTrail(XnaVector2 shipCenter, System.Numerics.Vector2 move, long tick)
    {
        if (move.LengthSquared() < 0.01f)
            return;
        var dir = System.Numerics.Vector2.Normalize(move);
        var back = new XnaVector2(-dir.X, -dir.Y);
        var flicker = (tick / 3) % 2 == 0;
        var length = flicker ? 18 : 14;
        for (var i = 1; i <= 3; i++)
        {
            var px = (int)(shipCenter.X + back.X * (8 + i * 5));
            var py = (int)(shipCenter.Y + back.Y * (8 + i * 5));
            var size = Math.Max(2, length / i - 2);
            Fill(px - size / 2, py - size / 2, size, size, new XnaColor((byte)255, (byte)180, (byte)80, (byte)(200 - i * 40)));
        }
    }

    private void DrawMuzzleFlash(XnaVector2 shipCenter, System.Numerics.Vector2 aim, long tick)
    {
        if (aim.LengthSquared() < 0.01f)
            return;
        var dir = System.Numerics.Vector2.Normalize(aim);
        var tip = new XnaVector2(shipCenter.X + dir.X * 18f, shipCenter.Y + dir.Y * 18f);
        var pulse = (tick % 4) < 2;
        Fill((int)tip.X - 2, (int)tip.Y - 2, pulse ? 5 : 3, pulse ? 5 : 3, new XnaColor(255, 230, 140));
    }

    private void DrawMineRay(XnaVector2 shipCenter, System.Numerics.Vector2 aim)
    {
        if (aim.LengthSquared() < 0.01f)
            return;
        var dir = System.Numerics.Vector2.Normalize(aim);
        for (var i = 1; i <= 8; i++)
        {
            var px = (int)(shipCenter.X + dir.X * (12 + i * 7));
            var py = (int)(shipCenter.Y + dir.Y * (12 + i * 7));
            Fill(px - 1, py - 1, 3, 3, new XnaColor((byte)120, (byte)220, (byte)255, (byte)(220 - i * 20)));
        }
    }

    private void DrawAimReticle(System.Numerics.Vector2 mouseVirtual)
    {
        var x = (int)mouseVirtual.X;
        var y = (int)mouseVirtual.Y;
        if (x < 0 || y < 0 || x >= VirtualWidth || y >= VirtualHeight)
            return;
        Fill(x - 6, y, 13, 1, new XnaColor(240, 240, 200));
        Fill(x, y - 6, 1, 13, new XnaColor(240, 240, 200));
        Frame(x - 4, y - 4, 9, 9, new XnaColor(220, 200, 120), 1);
    }

    /// <summary>
    /// Pixel pointer with tip at the virtual mouse position. Drawn last so it sits above UI/world.
    /// </summary>
    private void DrawMouseCursor(System.Numerics.Vector2 mouseVirtual, bool pressed)
    {
        var x = (int)mouseVirtual.X;
        var y = (int)mouseVirtual.Y;
        if (x < 0 || y < 0 || x >= VirtualWidth || y >= VirtualHeight)
            return;

        var fill = pressed ? new XnaColor(255, 230, 140) : new XnaColor(245, 248, 255);
        var outline = new XnaColor(20, 24, 32);

        // Classic 1-pixel tip arrow (hotspot at tip).
        ReadOnlySpan<(int Ox, int Oy, int W)> rows =
        [
            (0, 0, 1),
            (0, 1, 2),
            (0, 2, 3),
            (0, 3, 4),
            (0, 4, 5),
            (0, 5, 6),
            (0, 6, 7),
            (0, 7, 4),
            (0, 8, 3),
            (2, 9, 2),
            (2, 10, 2),
            (3, 11, 2)
        ];

        foreach (var (ox, oy, w) in rows)
        {
            Fill(x + ox - 1, y + oy, w + 2, 1, outline);
        }

        foreach (var (ox, oy, w) in rows)
        {
            if (oy == 0)
            {
                Fill(x, y, 1, 1, fill);
                continue;
            }

            Fill(x + ox, y + oy, w, 1, fill);
        }
    }

    private void DrawBar(int x, int y, int width, int height, float ratio, XnaColor fill)
    {
        ratio = Math.Clamp(ratio, 0f, 1f);
        Fill(x, y, width, height, new XnaColor(30, 36, 44));
        Frame(x, y, width, height, new XnaColor(90, 100, 110), 1);
        var inner = Math.Max(0, (int)((width - 2) * ratio));
        if (inner > 0)
            Fill(x + 1, y + 1, inner, height - 2, fill);
    }

    private void UpdateCombatFlash(ComposedRunOrchestrator run, RunPresentationHints hints)
    {
        if (!hints.FlashesEnabled)
        {
            _flashAlpha = 0f;
            return;
        }

        var cues = _combatCues.Translate(run.LastCombatEvents, id =>
        {
            try
            {
                return run.Combat.Snapshot(id);
            }
            catch (Exception)
            {
                return null;
            }
        });

        foreach (var cue in cues)
        {
            if (cue.Tick == _lastFlashTick && cue.Kind is not (CombatCueKind.Hull or CombatCueKind.ShieldBreak))
                continue;
            switch (cue.Kind)
            {
                case CombatCueKind.Hull:
                    _flashColor = new XnaColor(220, 60, 60);
                    _flashAlpha = 0.35f;
                    _lastFlashTick = cue.Tick;
                    break;
                case CombatCueKind.Shield or CombatCueKind.ShieldBreak:
                    _flashColor = new XnaColor(60, 160, 220);
                    _flashAlpha = 0.28f;
                    _lastFlashTick = cue.Tick;
                    break;
                case CombatCueKind.Weapon:
                    _flashColor = new XnaColor(255, 220, 120);
                    _flashAlpha = MathF.Max(_flashAlpha, 0.12f);
                    break;
                case CombatCueKind.Mobility:
                    _flashColor = new XnaColor(180, 255, 200);
                    _flashAlpha = MathF.Max(_flashAlpha, 0.18f);
                    break;
            }
        }

        foreach (var worldEvent in run.LastWorldEvents)
        {
            var bound = WorldRunPresentationBindings.Bind(worldEvent);
            if (bound is null)
                continue;
            if (worldEvent.Kind is WorldRunEventKind.ExtractionActivated or WorldRunEventKind.ObjectiveCompleted)
            {
                _flashColor = new XnaColor(140, 220, 160);
                _flashAlpha = MathF.Max(_flashAlpha, 0.22f);
            }
            else if (worldEvent.Kind is WorldRunEventKind.HazardWarned or WorldRunEventKind.CollapseWarning)
            {
                _flashColor = new XnaColor(240, 160, 60);
                _flashAlpha = MathF.Max(_flashAlpha, 0.25f);
            }
        }
    }

    private void DrawPanel(string title, params string[] lines)
    {
        Fill(40, 40, 560, 40 + lines.Length * 18, new XnaColor(12, 18, 28, 230));
        Frame(40, 40, 560, 40 + lines.Length * 18, new XnaColor(200, 210, 230), 1);
        DrawText(56, 52, title, new XnaColor(230, 240, 255), 2);
        var y = 80;
        foreach (var line in lines)
        {
            DrawText(56, y, line, XnaColor.White);
            y += 18;
        }
    }

    private void DrawText(int x, int y, string text, XnaColor color, int scale = 1)
    {
        _font.Draw(_spriteBatch, text, x, y, color, scale);
        _drewSprites = true;
    }

    private void DrawRegion(string regionId, int x, int y, int width, int height)
    {
        if (!TryGetRegionTexture(regionId, out var region, out var texture))
        {
            Fill(x, y, width, height, new XnaColor(160, 120, 80));
            _drewSprites = true;
            return;
        }

        var source = new XnaRectangle(region.X, region.Y, region.Width, region.Height);
        _spriteBatch.Draw(texture, new XnaRectangle(x, y, width, height), source, XnaColor.White);
        _drewSprites = true;
        _drewAtlasRegion = true;
    }

    private void DrawRegionRotated(string regionId, XnaVector2 center, float rotationRadians, int size)
    {
        if (!TryGetRegionTexture(regionId, out var region, out var texture))
        {
            Fill((int)center.X - size / 2, (int)center.Y - size / 2, size, size, new XnaColor(160, 120, 80));
            _drewSprites = true;
            return;
        }

        var source = new XnaRectangle(region.X, region.Y, region.Width, region.Height);
        var origin = new XnaVector2(region.Width * (float)region.PivotX, region.Height * (float)region.PivotY);
        var rotation = rotationRadians + MathF.PI / 2f;
        _spriteBatch.Draw(
            texture,
            center,
            source,
            XnaColor.White,
            rotation,
            origin,
            size / (float)Math.Max(region.Width, region.Height),
            SpriteEffects.None,
            0f);
        _drewSprites = true;
        _drewAtlasRegion = true;
    }

    private bool TryGetRegionTexture(string regionId, out AtlasRegion region, out Texture2D texture)
    {
        region = default!;
        texture = null!;
        try
        {
            region = _catalog.GetRegion(regionId);
        }
        catch (KeyNotFoundException)
        {
            return false;
        }

        var textureId = FindAtlasTexture(regionId);
        return textureId is not null && _textures.TryGetValue(textureId, out texture!);
    }

    private void DrawTexture(string assetId, int x, int y, int width, int height, XnaColor color)
    {
        if (!_textures.TryGetValue(assetId, out var texture))
        {
            Fill(x, y, width, height, new XnaColor(20, 30, 40));
            return;
        }

        _spriteBatch.Draw(texture, new XnaRectangle(x, y, width, height), color);
        _drewSprites = true;
    }

    private static string? FindAtlasTexture(string regionId)
    {
        if (regionId.StartsWith("ships/", StringComparison.Ordinal) ||
            regionId.StartsWith("modules/", StringComparison.Ordinal))
            return "atlases/player-modules";
        if (regionId.StartsWith("enemies/", StringComparison.Ordinal) ||
            regionId.StartsWith("telegraphs/", StringComparison.Ordinal) ||
            regionId.StartsWith("effects/", StringComparison.Ordinal) ||
            regionId.StartsWith("projectiles/", StringComparison.Ordinal))
            return "atlases/enemies-telegraphs";
        if (regionId.StartsWith("asteroids/", StringComparison.Ordinal) ||
            regionId.StartsWith("pickups/", StringComparison.Ordinal) ||
            regionId.StartsWith("field/", StringComparison.Ordinal) ||
            regionId.StartsWith("hazards/", StringComparison.Ordinal))
            return "atlases/asteroids-resources";
        if (regionId.StartsWith("ui/", StringComparison.Ordinal))
            return "atlases/ui-icons";
        return null;
    }

    private void Fill(int x, int y, int width, int height, XnaColor color) =>
        _spriteBatch.Draw(_pixel, new XnaRectangle(x, y, width, height), color);

    private void Frame(int x, int y, int width, int height, XnaColor color, int thickness)
    {
        Fill(x, y, width, thickness, color);
        Fill(x, y + height - thickness, width, thickness, color);
        Fill(x, y, thickness, height, color);
        Fill(x + width - thickness, y, thickness, height, color);
        _drewSprites = true;
    }

    private static UiControl? FindControl(UiShell ui, string id)
    {
        foreach (var control in ui.Controls)
        {
            if (string.Equals(control.Id, id, StringComparison.Ordinal))
                return control;
        }

        return null;
    }

    private static string UpgradeIcon(string upgradeId) => upgradeId switch
    {
        "UPG_OVERCHARGED_MUNITIONS" => "ui/icons/upgrade-damage",
        "UPG_RAPID_CYCLING" => "ui/icons/upgrade-rate",
        "UPG_FORKED_OUTPUT" => "ui/icons/upgrade-fork",
        "UPG_PENETRATING_FIELD" => "ui/icons/upgrade-pierce",
        "UPG_SHIELD_RESERVOIR" => "ui/icons/upgrade-shield",
        "UPG_FAST_REBOOT" => "ui/icons/upgrade-reboot",
        "UPG_REINFORCED_FRAME" => "ui/icons/upgrade-hull",
        "UPG_THRUSTER_OVERCLOCK" => "ui/icons/upgrade-speed",
        "UPG_MOBILITY_LOOP" => "ui/icons/upgrade-mobility",
        "UPG_FRACTURE_LENS" => "ui/icons/upgrade-mining",
        "UPG_MAGNETIC_SWEEP" => "ui/icons/upgrade-tractor",
        "UPG_SHOCK_TRANSIT" => "ui/icons/upgrade-shock",
        _ => "ui/icons/objective"
    };

    private static XnaColor ScreenBackground(MetaScreen screen) => screen switch
    {
        MetaScreen.Title => new XnaColor(10, 16, 32),
        MetaScreen.Station or MetaScreen.Upgrades => new XnaColor(14, 28, 36),
        MetaScreen.Run => new XnaColor(6, 10, 18),
        MetaScreen.Summary => new XnaColor(28, 18, 36),
        _ => new XnaColor(12, 14, 22)
    };

    private static XnaVector2 WorldToScreen(System.Numerics.Vector2 world, System.Numerics.Vector2 camera)
    {
        var x = (int)MathF.Round(world.X - camera.X + VirtualWidth / 2f);
        var y = (int)MathF.Round(world.Y - camera.Y + VirtualHeight / 2f);
        return new XnaVector2(x, y);
    }

    private static bool OnScreen(XnaVector2 screen, int margin) =>
        screen.X >= -margin && screen.X <= VirtualWidth + margin &&
        screen.Y >= -margin && screen.Y <= VirtualHeight + margin;

    public static string ShortId(string id)
    {
        var value = id;
        foreach (var prefix in new[] { "MOD_", "RES_", "ENV_", "UPG_", "MAT_", "CAP_" })
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
            {
                value = value[prefix.Length..];
                break;
            }
        }

        return value.Replace('_', ' ');
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..Math.Max(0, max - 3)] + "...";

    public void Dispose()
    {
        _spriteBatch.Dispose();
        _font.Dispose();
        _pixel.Dispose();
    }
}
