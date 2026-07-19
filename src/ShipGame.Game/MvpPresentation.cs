using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShipGame.Content;
using ShipGame.Domain;
using ShipGame.Gameplay;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace ShipGame.Game;

/// <summary>
/// 640×360 integer-scaled presentation bound to catalog atlas regions (P5).
/// </summary>
public sealed class MvpPresentation : IMetaScreenCanvas, IDisposable
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
    private readonly ParticleSystem _particles = new();
    private readonly MetaScreenHandlerRegistry _screenHandlers;
    private bool _drewSprites;
    private bool _drewAtlasRegion;
    private int _texturesLoaded;
    private float _flashAlpha;
    private XnaColor _flashColor = XnaColor.White;
    private long _lastFlashTick = -1;
    private long _particlesSpawnedForCombatTick = -1;
    private long _lastMiningSparkTick = -1;
    private long _lastBeamSparkTick = -1;
    private string? _phaseToast;
    private int _phaseToastFrames;

    public MvpPresentation(
        GraphicsDevice device,
        RuntimeContentCatalog catalog,
        MetaScreenHandlerRegistry screenHandlers)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _screenHandlers = screenHandlers ?? throw new ArgumentNullException(nameof(screenHandlers));
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
        _screenHandlers.Draw(
            screen,
            new MetaDrawContext
            {
                Session = session,
                Run = run,
                Ui = ui,
                Hints = hints,
                Canvas = this
            });

        Frame(0, 0, VirtualWidth, VirtualHeight, new XnaColor(180, 200, 220), 2);
        if (hints.ShowCursor)
            DrawMouseCursor(hints.MouseVirtual, hints.FireHeld || ui.PressedId is not null);
        _spriteBatch.End();
    }

    public void DrawText(int x, int y, string text, XnaColor color, int scale = 1)
    {
        _font.Draw(_spriteBatch, text, x, y, color, scale);
        _drewSprites = true;
    }

    public void DrawRegion(string regionId, int x, int y, int width, int height)
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

    public void DrawRegionRotated(string regionId, XnaVector2 center, float rotationRadians, int size)
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

    public void DrawTexture(string assetId, int x, int y, int width, int height, XnaColor color)
    {
        if (!_textures.TryGetValue(assetId, out var texture))
        {
            Fill(x, y, width, height, new XnaColor(20, 30, 40));
            return;
        }

        _spriteBatch.Draw(texture, new XnaRectangle(x, y, width, height), color);
        _drewSprites = true;
    }

    public void DrawShellButtons(UiShell ui, string? skipPrefix = null)
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

    public void DrawButton(UiRect bounds, string label, UiControlState state)
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

    public void DrawBankedPurse(MetaSession session)
    {
        var balances = session.Station.Balances;
        DrawRegion("ui/icons/resource-ferrite", 400, 16, 14, 14);
        DrawText(418, 18, $"{balances.Ferrite}", new XnaColor(220, 200, 160));
        DrawRegion("ui/icons/resource-lumen", 470, 16, 14, 14);
        DrawText(488, 18, $"{balances.Lumen}", new XnaColor(180, 220, 255));
        DrawRegion("ui/icons/resource-data-core", 530, 16, 14, 14);
        DrawText(548, 18, $"{balances.DataCores}", new XnaColor(200, 180, 255));
    }

    public void DrawPanel(string title, params string[] lines)
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

    public void DrawParallaxBackground(string bgId, System.Numerics.Vector2 camera)
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

    public void DrawThrustTrail(XnaVector2 shipCenter, System.Numerics.Vector2 move, long tick)
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

    public void DrawMuzzleFlash(XnaVector2 shipCenter, System.Numerics.Vector2 aim, long tick)
    {
        if (aim.LengthSquared() < 0.01f)
            return;
        var dir = System.Numerics.Vector2.Normalize(aim);
        var tip = new XnaVector2(shipCenter.X + dir.X * 18f, shipCenter.Y + dir.Y * 18f);
        var pulse = (tick % 4) < 2;
        Fill((int)tip.X - 2, (int)tip.Y - 2, pulse ? 5 : 3, pulse ? 5 : 3, new XnaColor(255, 230, 140));
    }

    public void DrawMineRay(XnaVector2 shipCenter, System.Numerics.Vector2 aim, float? hitDistanceWorld = null)
    {
        if (aim.LengthSquared() < 0.01f)
            return;
        const float muzzle = 14f;
        var dir = System.Numerics.Vector2.Normalize(aim);
        var start = new XnaVector2(shipCenter.X + dir.X * muzzle, shipCenter.Y + dir.Y * muzzle);
        var worldLength = hitDistanceWorld is > 0 and var hit
            ? MathF.Max(0f, hit - muzzle)
            : 36f;
        var length = Math.Clamp(worldLength, 16f, ComposedRunOrchestrator.MiningRangeWorldUnits + 8f);
        var rotation = MathF.Atan2(dir.Y, dir.X);
        DrawRotatedBeamLayer(start, length, rotation, thickness: 6f, new XnaColor(40, 140, 180, 60));
        DrawRotatedBeamLayer(start, length, rotation, thickness: 3f, new XnaColor(90, 210, 240, 170));
        DrawRotatedBeamLayer(start, length, rotation, thickness: 1f, new XnaColor(200, 250, 255, 230));
        Fill((int)start.X - 2, (int)start.Y - 2, 5, 5, new XnaColor(180, 240, 255));
        if (hitDistanceWorld is > 0)
        {
            var tip = new XnaVector2(start.X + dir.X * length, start.Y + dir.Y * length);
            Fill((int)tip.X - 2, (int)tip.Y - 2, 5, 5, new XnaColor(160, 230, 255));
        }

        _drewSprites = true;
    }

    public void DrawBeamRay(
        XnaVector2 shipCenter,
        System.Numerics.Vector2 aim,
        float rangeWorld,
        float? hitDistanceWorld = null)
    {
        if (aim.LengthSquared() < 0.01f)
            return;
        var dir = System.Numerics.Vector2.Normalize(aim);
        const float muzzle = 14f;
        var start = new XnaVector2(shipCenter.X + dir.X * muzzle, shipCenter.Y + dir.Y * muzzle);
        // Hit distance is from ship center to surface; subtract muzzle so the tip lands on the hit.
        var worldLength = hitDistanceWorld is > 0 and var hit
            ? MathF.Max(0f, MathF.Min(hit, rangeWorld) - muzzle)
            : rangeWorld;
        var length = Math.Clamp(worldLength, 40f, 340f);
        var rotation = MathF.Atan2(dir.Y, dir.X);
        DrawRotatedBeamLayer(start, length, rotation, thickness: 7f, new XnaColor(255, 140, 40, 70));
        DrawRotatedBeamLayer(start, length, rotation, thickness: 3f, new XnaColor(255, 200, 90, 180));
        DrawRotatedBeamLayer(start, length, rotation, thickness: 1f, new XnaColor(255, 245, 210, 240));
        var tip = new XnaVector2(start.X + dir.X * length, start.Y + dir.Y * length);
        Fill((int)start.X - 2, (int)start.Y - 2, 5, 5, new XnaColor(255, 240, 180));
        if (hitDistanceWorld is > 0)
            Fill((int)tip.X - 2, (int)tip.Y - 2, 5, 5, new XnaColor(255, 220, 120));
        _drewSprites = true;
    }

    private void DrawRotatedBeamLayer(XnaVector2 start, float length, float rotation, float thickness, XnaColor color)
    {
        _spriteBatch.Draw(
            _pixel,
            start,
            null,
            color,
            rotation,
            new XnaVector2(0f, 0.5f),
            new XnaVector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    public void DrawAimReticle(System.Numerics.Vector2 mouseVirtual)
    {
        var x = (int)mouseVirtual.X;
        var y = (int)mouseVirtual.Y;
        if (x < 0 || y < 0 || x >= VirtualWidth || y >= VirtualHeight)
            return;
        Fill(x - 6, y, 13, 1, new XnaColor(240, 240, 200));
        Fill(x, y - 6, 1, 13, new XnaColor(240, 240, 200));
        Frame(x - 4, y - 4, 9, 9, new XnaColor(220, 200, 120), 1);
    }

    public void DrawRunHud(ComposedRunHud hud, RunPresentationHints hints, XnaVector2 playerScreen)
    {
        var briefing = RunObjectiveBriefing.For(hud);
        Fill(0, 0, VirtualWidth, 72, new XnaColor(0, 0, 0, 180));
        DrawRegion("ui/icons/hull", 8, 6, 16, 16);
        DrawBar(28, 8, 90, 10, hints.MaxHull <= 0 ? 0 : hud.Hull / hints.MaxHull, new XnaColor(200, 90, 90));
        DrawText(122, 6, $"{hud.Hull:0}", XnaColor.White);

        DrawRegion("ui/icons/shield", 160, 6, 16, 16);
        DrawBar(180, 8, 90, 10, hints.MaxShield <= 0 ? 0 : hud.Shield / Math.Max(1f, hints.MaxShield), new XnaColor(80, 180, 220));
        DrawText(274, 6, $"{hud.Shield:0}", XnaColor.White);

        DrawRegion("ui/icons/resource-ferrite", 320, 6, 16, 16);
        DrawText(340, 6, $"{hud.FerriteHeld}", new XnaColor(220, 200, 160));
        DrawRegion("ui/icons/resource-lumen", 380, 6, 16, 16);
        DrawText(400, 6, $"{hud.LumenHeld}", new XnaColor(140, 210, 230));
        DrawRegion("ui/icons/resource-data-core", 440, 6, 16, 16);
        DrawText(460, 6, $"{hud.DataCoresHeld}", new XnaColor(200, 180, 255));

        DrawText(8, 26, briefing.Title, new XnaColor(220, 230, 200));
        DrawText(8, 40, Truncate(briefing.Body, 78), new XnaColor(180, 200, 180));
        if (briefing.Controls.Length > 0)
            DrawText(8, 54, briefing.Controls, new XnaColor(150, 170, 190));

        if (hints.MoveIntent.LengthSquared() > 0.01f)
        {
            var dir = System.Numerics.Vector2.Normalize(hints.MoveIntent);
            var tip = new XnaVector2(playerScreen.X + dir.X * 22f, playerScreen.Y + dir.Y * 22f);
            Fill((int)tip.X - 2, (int)tip.Y - 2, 4, 4, new XnaColor(120, 220, 160));
        }
    }

    public void DrawEdgePing(string regionId, EdgePing ping, int size, string? label = null)
    {
        DrawRegionRotated(regionId, ping.ScreenPosition, ping.RotationRadians, size);
        if (label is not null)
            DrawText((int)ping.ScreenPosition.X - 16, (int)ping.ScreenPosition.Y + size / 2 + 2, label, new XnaColor(220, 220, 160));
    }

    public void DrawPhaseToast()
    {
        if (_phaseToastFrames <= 0 || _phaseToast is null)
            return;
        Fill(80, 86, VirtualWidth - 160, 22, new XnaColor(0, 0, 0, 160));
        DrawText(88, 92, Truncate(_phaseToast, 70), new XnaColor(200, 230, 180));
        _phaseToastFrames--;
    }

    public void UpdateCombatFlash(ComposedRunOrchestrator run, RunPresentationHints hints)
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
            var toast = RunObjectiveBriefing.ToastFor(worldEvent.Kind);
            if (toast is not null)
            {
                _phaseToast = toast;
                _phaseToastFrames = 150;
            }

            var bound = WorldRunPresentationBindings.Bind(worldEvent);
            if (bound is null)
                continue;
            if (worldEvent.Kind is WorldRunEventKind.ExtractionActivated or WorldRunEventKind.ObjectiveCompleted
                or WorldRunEventKind.EliteActivationRequested)
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

    public void UpdateRunParticles(
        ComposedRunOrchestrator run,
        RunPresentationHints hints,
        System.Numerics.Vector2 camera,
        float deltaSeconds,
        bool paused)
    {
        ArgumentNullException.ThrowIfNull(run);
        _ = camera;

        if (!hints.ParticlesEnabled)
        {
            _particles.Clear();
            return;
        }

        var combatTick = run.Combat.Tick;
        if (combatTick != _particlesSpawnedForCombatTick)
        {
            _particlesSpawnedForCombatTick = combatTick;
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
                SpawnParticlesForCue(cue);

            foreach (var broken in run.LastBrokenAsteroids)
                _particles.Burst(broken.Position, ParticlePresets.AsteroidBreak(broken.Kind));
        }

        var mining = run.LastMiningPresentation;
        if (!paused &&
            mining.Active &&
            mining.Hit &&
            combatTick != _lastMiningSparkTick &&
            combatTick % 3 == 0)
        {
            _lastMiningSparkTick = combatTick;
            _particles.Burst(mining.HitPosition, ParticlePresets.MiningSparks);
        }

        if (!paused &&
            hints.FireHeld &&
            run.Combat.TryGetPlayerWeapon(out var weapon, out _) &&
            weapon == WeaponBehavior.Beam &&
            run.Combat.TryGetPlayerBeamHitDistance(out var beamHit) &&
            beamHit > 0 &&
            combatTick != _lastBeamSparkTick &&
            combatTick % 2 == 0)
        {
            _lastBeamSparkTick = combatTick;
            var player = run.Combat.Player != default
                ? run.Combat.Snapshot(run.Combat.Player).Position
                : System.Numerics.Vector2.Zero;
            var aim = hints.AimDirection.LengthSquared() > 0.01f
                ? System.Numerics.Vector2.Normalize(hints.AimDirection)
                : System.Numerics.Vector2.UnitX;
            // Match DrawBeamRay: tip is at ship-center hit distance along aim.
            _particles.Burst(player + aim * beamHit, ParticlePresets.BeamTip);
        }

        if (!paused)
            _particles.Update(Math.Max(0f, deltaSeconds));
    }

    public void DrawParticles(System.Numerics.Vector2 camera)
    {
        var span = _particles.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            ref readonly var particle = ref span[i];
            if (!particle.Active)
                continue;

            var screen = WorldToScreen(particle.Position, camera);
            if (!OnScreen(screen, 8))
                continue;

            var fade = Math.Clamp(particle.Life / Math.Max(0.001f, particle.MaxLife), 0f, 1f);
            var color = particle.Color * fade;
            var size = particle.Size;
            Fill((int)screen.X - size / 2, (int)screen.Y - size / 2, size, size, color);
        }

        if (_particles.ActiveCount > 0)
            _drewSprites = true;
    }

    private void SpawnParticlesForCue(CombatCue cue)
    {
        switch (cue.Kind)
        {
            case CombatCueKind.Impact:
                _particles.Burst(cue.Position, ParticlePresets.Impact);
                break;
            case CombatCueKind.Shield:
                _particles.Burst(cue.Position, ParticlePresets.ShieldHit);
                break;
            case CombatCueKind.ShieldBreak:
                _particles.Burst(cue.Position, ParticlePresets.ShieldBreak);
                break;
            case CombatCueKind.Hull:
                _particles.Burst(cue.Position, ParticlePresets.HullHit);
                break;
            case CombatCueKind.Destruction:
                _particles.Burst(cue.Position, ParticlePresets.Destruction);
                break;
        }
    }

    public void DrawRunFlashOverlay(RunPresentationHints hints)
    {
        if (_flashAlpha > 0.01f && hints.FlashesEnabled)
        {
            Fill(0, 0, VirtualWidth, VirtualHeight, _flashColor * _flashAlpha);
            _flashAlpha = MathF.Max(0f, _flashAlpha - 0.08f);
        }
    }

    public void Fill(int x, int y, int width, int height, XnaColor color) =>
        _spriteBatch.Draw(_pixel, new XnaRectangle(x, y, width, height), color);

    public void Frame(int x, int y, int width, int height, XnaColor color, int thickness)
    {
        Fill(x, y, width, thickness, color);
        Fill(x, y + height - thickness, width, thickness, color);
        Fill(x, y, thickness, height, color);
        Fill(x + width - thickness, y, thickness, height, color);
        _drewSprites = true;
    }

    public UiControl? FindControl(UiShell ui, string id)
    {
        foreach (var control in ui.Controls)
        {
            if (string.Equals(control.Id, id, StringComparison.Ordinal))
                return control;
        }

        return null;
    }

    public string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..Math.Max(0, max - 3)] + "...";

    public XnaVector2 WorldToScreen(System.Numerics.Vector2 world, System.Numerics.Vector2 camera)
    {
        var x = (int)MathF.Round(world.X - camera.X + VirtualWidth / 2f);
        var y = (int)MathF.Round(world.Y - camera.Y + VirtualHeight / 2f);
        return new XnaVector2(x, y);
    }

    public bool OnScreen(XnaVector2 screen, int margin) =>
        screen.X >= -margin && screen.X <= VirtualWidth + margin &&
        screen.Y >= -margin && screen.Y <= VirtualHeight + margin;

    private void DrawMouseCursor(System.Numerics.Vector2 mouseVirtual, bool pressed)
    {
        var x = (int)mouseVirtual.X;
        var y = (int)mouseVirtual.Y;
        if (x < 0 || y < 0 || x >= VirtualWidth || y >= VirtualHeight)
            return;

        var fill = pressed ? new XnaColor(255, 230, 140) : new XnaColor(245, 248, 255);
        var outline = new XnaColor(20, 24, 32);

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

    private static XnaColor ScreenBackground(MetaScreen screen) => screen switch
    {
        MetaScreen.Title => new XnaColor(10, 16, 32),
        MetaScreen.Station or MetaScreen.Upgrades => new XnaColor(14, 28, 36),
        MetaScreen.Run => new XnaColor(6, 10, 18),
        MetaScreen.Summary => new XnaColor(28, 18, 36),
        _ => new XnaColor(12, 14, 22)
    };

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

    public void Dispose()
    {
        _spriteBatch.Dispose();
        _font.Dispose();
        _pixel.Dispose();
    }
}
