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
    private bool _drewSprites;
    private bool _drewAtlasRegion;
    private int _texturesLoaded;

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
                DrawTitle();
                break;
            case MetaScreen.Lobby:
                DrawLobby(session);
                break;
            case MetaScreen.Map:
                DrawMap(session);
                break;
            case MetaScreen.Loadout:
                DrawLoadout(session);
                break;
            case MetaScreen.Research:
                DrawResearch(session);
                break;
            case MetaScreen.Summary:
                DrawSummary(session);
                break;
            case MetaScreen.Settings:
                DrawPanel("SETTINGS", "Telemetry and volumes.", "[Esc] back");
                break;
            case MetaScreen.Pause:
                DrawPanel("PAUSED", "Simulation clock stopped.", "[Esc] resume");
                break;
            case MetaScreen.Run:
                DrawRun(run);
                break;
        }

        Frame(0, 0, VirtualWidth, VirtualHeight, new XnaColor(180, 200, 220), 2);
        _spriteBatch.End();
    }

    private void DrawTitle()
    {
        DrawRegion("ui/icons/objective", 296, 48, 48, 48);
        DrawText(220, 110, "SHIP GAME", new XnaColor(240, 245, 255), 2);
        DrawPanel(
            "TITLE",
            "Enter  start / lobby",
            "C      continue save",
            "Esc    quit");
        DrawRegion("ui/icons/interact", 304, 300, 32, 32);
    }

    private void DrawLobby(MetaSession session)
    {
        var lobby = session.Lobby;
        DrawText(24, 16, "LOBBY", new XnaColor(230, 240, 255), 2);
        DrawRegion("ships/player/wayfarer", 520, 40, 72, 72);
        DrawText(24, 48, $"Ferrite {lobby.Balances.Ferrite}", new XnaColor(220, 200, 160));
        DrawText(24, 64, $"Lumen   {lobby.Balances.Lumen}", new XnaColor(180, 220, 255));
        DrawText(24, 80, $"Cores   {lobby.Balances.DataCores}", new XnaColor(200, 180, 255));
        if (lobby.PreviousRun is { } previous)
            DrawText(
                24,
                104,
                previous.Succeeded ? "Last run: EXTRACTED" : "Last run: FAILED",
                previous.Succeeded ? new XnaColor(140, 220, 160) : new XnaColor(220, 140, 140));

        DrawText(24, 160, "Controls", new XnaColor(180, 200, 220));
        DrawText(24, 180, "M  environment map / launch", XnaColor.White);
        DrawText(24, 196, "L  loadout", XnaColor.White);
        DrawText(24, 212, "R  research", XnaColor.White);
        DrawText(24, 228, "Enter  open map", XnaColor.White);
        DrawText(24, 260, "Goal: mine Ferrite, fight, extract.", new XnaColor(180, 200, 160));
    }

    private void DrawMap(MetaSession session)
    {
        DrawText(24, 16, "SELECT ENVIRONMENT", new XnaColor(230, 240, 255), 2);
        var y = 56;
        foreach (var env in session.Map)
        {
            var label = env.EnvironmentId.Replace("ENV_", "", StringComparison.Ordinal);
            var color = env.Accessible ? new XnaColor(140, 220, 180) : new XnaColor(120, 120, 130);
            Fill(24, y, 592, 44, color * 0.25f);
            if (env.Selected)
                Frame(24, y, 592, 44, new XnaColor(220, 230, 240), 1);
            DrawText(36, y + 8, env.Selected ? $"> {label}" : $"  {label}", color);
            DrawText(36, y + 24, env.Accessible ? "Enter to launch" : Truncate(env.Explanation, 70), new XnaColor(180, 180, 190));
            y += 52;
        }

        DrawText(24, 320, "Esc back to lobby", new XnaColor(160, 170, 180));
    }

    private void DrawLoadout(MetaSession session)
    {
        var loadout = session.Profile.ResolveLoadout().Effective;
        DrawText(24, 16, "LOADOUT", new XnaColor(230, 240, 255), 2);
        DrawRegion("ships/player/wayfarer", 520, 40, 72, 72);
        DrawText(24, 56, $"Weapon  {ShortId(loadout.Weapon)}", XnaColor.White);
        DrawText(24, 72, $"Mining  {ShortId(loadout.Mining)}", XnaColor.White);
        DrawText(24, 88, $"Shield  {ShortId(loadout.Shield)}", XnaColor.White);
        DrawText(24, 104, $"Engine  {ShortId(loadout.Engine)}", XnaColor.White);
        DrawText(24, 120, $"Utility {ShortId(loadout.Utility)}", XnaColor.White);
        DrawText(24, 160, "Equip changes persist on save.", new XnaColor(180, 200, 160));
        DrawText(24, 320, "Esc back", new XnaColor(160, 170, 180));
    }

    private void DrawResearch(MetaSession session)
    {
        DrawText(24, 16, "RESEARCH", new XnaColor(230, 240, 255), 2);
        var y = 48;
        foreach (var node in session.Research.Take(8))
        {
            var tint = node.Purchased ? new XnaColor(120, 200, 150) :
                node.Affordable && node.PrerequisitesMet && node.GateMet
                    ? new XnaColor(220, 200, 120)
                    : new XnaColor(120, 120, 130);
            DrawText(24, y, ShortId(node.Definition.Id), tint);
            DrawText(
                280,
                y,
                node.Purchased ? "OWNED" :
                node.Affordable && node.PrerequisitesMet && node.GateMet ? "READY" : "LOCKED",
                tint);
            y += 16;
        }

        DrawText(24, 300, "Purchases use banked resources after runs.", new XnaColor(180, 200, 160));
        DrawText(24, 320, "Esc back", new XnaColor(160, 170, 180));
    }

    private void DrawSummary(MetaSession session)
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
        DrawText(24, 320, "Enter  return to lobby", new XnaColor(180, 200, 220));
    }

    private void DrawRun(ComposedRunOrchestrator? run)
    {
        if (run is null)
        {
            DrawPanel("RUN", "Composing encounter...", "Please wait");
            return;
        }

        var bgId = run.EnvironmentId.Value == MetaContentIds.IonVeil
            ? "backgrounds/ion-veil"
            : "backgrounds/cinder-belt";
        DrawTexture(bgId, 0, 0, VirtualWidth, VirtualHeight, XnaColor.White * 0.9f);

        var hud = run.Hud;
        var camera = run.Combat.Player != default
            ? run.Combat.Snapshot(run.Combat.Player).Position
            : System.Numerics.Vector2.Zero;

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

        foreach (var item in run.LiveRenderItems)
        {
            var screen = WorldToScreen(item.Position, camera);
            if (!OnScreen(screen, 40))
                continue;
            switch (item.Kind)
            {
                case CombatRenderKind.PlayerShip:
                    DrawRegionRotated("ships/player/wayfarer", screen, item.Rotation, 32);
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

        // Extraction marker
        var extract = WorldToScreen(
            new System.Numerics.Vector2(
                run.Descriptor.Extraction.Center.X * FieldDescriptor.WorldUnitsPerCell,
                run.Descriptor.Extraction.Center.Y * FieldDescriptor.WorldUnitsPerCell),
            camera);
        if (OnScreen(extract, 40))
            DrawRegion("field/extraction-marker", (int)extract.X - 16, (int)extract.Y - 16, 32, 32);

        Fill(0, 0, VirtualWidth, 40, new XnaColor(0, 0, 0, 180));
        DrawText(
            8,
            6,
            $"Hull {hud.Hull:0}  Shield {hud.Shield:0}  Ferrite {hud.FerriteHeld}  Obj {hud.ObjectiveFerrite}/30 kills {hud.ObjectiveKills}/8",
            XnaColor.White);
        DrawText(
            8,
            22,
            $"{hud.Phase}  t{hud.RunTick}  WASD move  LMB fire  RMB mine  Space dash  E extract  Esc pause",
            new XnaColor(180, 200, 220));

        if (hud.PendingOffer is not null)
        {
            Fill(120, 120, 400, 80, new XnaColor(10, 14, 22, 230));
            Frame(120, 120, 400, 80, new XnaColor(220, 200, 120), 1);
            DrawText(140, 140, "UPGRADE OFFER", new XnaColor(240, 220, 140), 2);
            DrawText(140, 168, "Press 1 / 2 / 3 to choose", XnaColor.White);
        }

        if (hud.Phase == RunPhase.Extraction)
            DrawText(
                8,
                44,
                $"Hold E in extract zone: {hud.ExtractionProgressTicks}/{hud.ExtractionHoldTicks}",
                new XnaColor(160, 220, 180));
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
        // Source art faces up (-Y); combat aim uses mathematical angle from +X.
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

    private static XnaColor ScreenBackground(MetaScreen screen) => screen switch
    {
        MetaScreen.Title => new XnaColor(10, 16, 32),
        MetaScreen.Lobby => new XnaColor(14, 28, 36),
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

    private static string ShortId(string id)
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
