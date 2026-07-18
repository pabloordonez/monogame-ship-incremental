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
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly Texture2D _pixel;
    private bool _drewSprites;

    public MvpPresentation(GraphicsDevice device, RuntimeContentCatalog catalog)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _spriteBatch = new SpriteBatch(device);
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData([XnaColor.White]);
    }

    public bool DrewSpritesThisFrame => _drewSprites;

    public void LoadTextures(Microsoft.Xna.Framework.Content.ContentManager content)
    {
        ArgumentNullException.ThrowIfNull(content);
        foreach (var assetId in _catalog.AssetIds)
        {
            var asset = _catalog.GetAsset(assetId);
            if (asset.Kind is not ("texture" or "atlas"))
                continue;
            try
            {
                _textures[assetId] = content.Load<Texture2D>(assetId);
            }
            catch (Exception)
            {
                // Missing optional texture stays absent; UI falls back to solid rectangles.
            }
        }
    }

    public void BeginFrame() => _drewSprites = false;

    public void DrawMetaScreen(
        MetaScreen screen,
        MetaSession session,
        ComposedRunOrchestrator? run,
        int backBufferWidth,
        int backBufferHeight)
    {
        BeginFrame();
        var scale = Math.Max(1, Math.Min(backBufferWidth / VirtualWidth, backBufferHeight / VirtualHeight));
        var destWidth = VirtualWidth * scale;
        var destHeight = VirtualHeight * scale;
        var offsetX = (backBufferWidth - destWidth) / 2;
        var offsetY = (backBufferHeight - destHeight) / 2;
        var letterbox = new XnaRectangle(offsetX, offsetY, destWidth, destHeight);

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
                DrawPanel("SETTINGS", "Telemetry and volumes. [Esc] back.");
                break;
            case MetaScreen.Pause:
                DrawPanel("PAUSED", "[Esc] resume  [1/2/3] unused");
                break;
            case MetaScreen.Run:
                DrawRun(session, run);
                break;
        }

        // Letterbox border cue (non-solid full-screen clear alone).
        Frame(0, 0, VirtualWidth, VirtualHeight, new XnaColor(180, 200, 220), 2);
        _spriteBatch.End();
        _ = letterbox;
    }

    private void DrawTitle()
    {
        DrawRegion("ui/icons/objective", 240, 80, 48, 48);
        DrawPanel("SHIP GAME", "Enter: New/Continue path   C: Continue   Esc: Exit");
        DrawRegion("ui/icons/interact", 300, 260, 32, 32);
    }

    private void DrawLobby(MetaSession session)
    {
        var lobby = session.Lobby;
        DrawPanel(
            "LOBBY",
            $"Ferrite {lobby.Balances.Ferrite}  Lumen {lobby.Balances.Lumen}  Cores {lobby.Balances.DataCores}");
        DrawRegion("ships/player/wayfarer", 280, 140, 64, 64);
        DrawLabel(24, 220, "M Map   L Loadout   R Research   Enter Launch");
    }

    private void DrawMap(MetaSession session)
    {
        DrawPanel("ENVIRONMENT SELECT", "Choose destination then Enter to launch.");
        var y = 120;
        foreach (var env in session.Map)
        {
            var color = env.Accessible ? new XnaColor(120, 200, 160) : new XnaColor(90, 90, 100);
            Fill(40, y, 560, 36, color * 0.35f);
            DrawLabel(52, y + 10, $"{env.EnvironmentId} {(env.Selected ? "<" : "")} {env.Explanation}");
            y += 48;
        }
    }

    private void DrawLoadout(MetaSession session)
    {
        var loadout = session.Profile.ResolveLoadout().Effective;
        DrawPanel("LOADOUT", $"{loadout.Weapon} / {loadout.Mining} / {loadout.Shield}");
        DrawRegion("ships/player/wayfarer", 288, 140, 64, 64);
        DrawLabel(24, 240, $"{loadout.Engine}  {loadout.Utility}   Esc: back");
    }

    private void DrawResearch(MetaSession session)
    {
        DrawPanel("RESEARCH", "Purchase with banked resources. Esc: back");
        var y = 110;
        foreach (var node in session.Research.Take(6))
        {
            var tint = node.Purchased ? new XnaColor(80, 160, 120) :
                node.Affordable && node.PrerequisitesMet && node.GateMet
                    ? new XnaColor(160, 160, 80)
                    : new XnaColor(70, 70, 80);
            Fill(40, y, 560, 28, tint * 0.4f);
            DrawLabel(52, y + 8, node.Definition.Id);
            y += 32;
        }
    }

    private void DrawSummary(MetaSession session)
    {
        var previous = session.Profile.Snapshot.PreviousRun;
        DrawPanel(
            "RUN SUMMARY",
            previous is null
                ? "No previous run."
                : $"{(previous.Succeeded ? "EXTRACTED" : "FAILED")}  banked F{previous.Banked.Ferrite} L{previous.Banked.Lumen} D{previous.Banked.DataCores}");
        DrawRegion("ui/icons/objective", 304, 180, 32, 32);
        DrawLabel(24, 260, "Enter: return to lobby");
    }

    private void DrawRun(MetaSession session, ComposedRunOrchestrator? run)
    {
        if (run is null)
        {
            DrawPanel("RUN", "Composing encounter…");
            return;
        }

        var bgId = run.EnvironmentId.Value == MetaContentIds.IonVeil
            ? "backgrounds/ion-veil"
            : "backgrounds/cinder-belt";
        DrawTexture(bgId, 0, 0, VirtualWidth, VirtualHeight, XnaColor.White * 0.85f);

        var hud = run.Hud;
        var player = run.Combat.Player != default
            ? run.Combat.Snapshot(run.Combat.Player)
            : default;
        var camera = player.Position;

        foreach (var asteroid in run.Asteroids)
        {
            if (asteroid.Broken)
                continue;
            var region = asteroid.Kind switch
            {
                AsteroidCellKind.Ferrite => "asteroids/medium/ferrite",
                AsteroidCellKind.Lumen => "asteroids/medium/lumen",
                _ => "asteroids/medium/ordinary"
            };
            var screen = WorldToScreen(new System.Numerics.Vector2(asteroid.X, asteroid.Y), camera);
            DrawRegion(region, (int)screen.X - 12, (int)screen.Y - 12, 24, 24);
        }

        foreach (var pickup in run.Pickups)
        {
            var screen = WorldToScreen(new System.Numerics.Vector2(pickup.X, pickup.Y), camera);
            DrawRegion("pickups/ferrite", (int)screen.X - 6, (int)screen.Y - 6, 12, 12);
        }

        foreach (var snapshot in run.LiveCombatSnapshots)
        {
            if (snapshot.Destroyed)
                continue;
            var screen = WorldToScreen(snapshot.Position, camera);
            var region = snapshot.Faction switch
            {
                Faction.Player => "ships/player/wayfarer",
                Faction.Hostile => "enemies/interceptor",
                _ => "enemies/elite-outline"
            };
            var size = snapshot.Faction == Faction.Player ? 28 : 20;
            DrawRegion(region, (int)screen.X - size / 2, (int)screen.Y - size / 2, size, size);
        }

        Fill(0, 0, VirtualWidth, 28, new XnaColor(0, 0, 0, 160));
        DrawLabel(
            8,
            8,
            $"H{hud.Hull:0} S{hud.Shield:0}  F{hud.FerriteHeld}  Obj {hud.ObjectiveFerrite}/30 {hud.ObjectiveKills}/8  {hud.Phase} t{hud.RunTick}");
        if (hud.PendingOffer is not null)
            DrawPanel("UPGRADE", "Press 1 / 2 / 3 to choose");
        if (hud.Phase == RunPhase.Extraction)
            DrawLabel(8, 34, $"Extract hold {hud.ExtractionProgressTicks}/{hud.ExtractionHoldTicks} (E)");
        _ = session;
    }

    private void DrawPanel(string title, string body)
    {
        Fill(40, 40, 560, 80, new XnaColor(12, 18, 28, 220));
        Frame(40, 40, 560, 80, new XnaColor(200, 210, 230), 1);
        DrawLabel(56, 56, title);
        DrawLabel(56, 84, body);
    }

    private void DrawLabel(int x, int y, string text)
    {
        // Bitmap-less MVP: draw a readable bar proportional to text length.
        var width = Math.Clamp(text.Length * 5, 16, VirtualWidth - x - 8);
        Fill(x, y, width, 10, new XnaColor(220, 230, 240));
        Fill(x, y + 12, Math.Min(width, 120), 3, new XnaColor(120, 160, 200));
        _drewSprites = true;
    }

    private void DrawRegion(string regionId, int x, int y, int width, int height)
    {
        AtlasRegion region;
        try
        {
            region = _catalog.GetRegion(regionId);
        }
        catch (KeyNotFoundException)
        {
            Fill(x, y, width, height, new XnaColor(180, 140, 90));
            _drewSprites = true;
            return;
        }

        var textureId = FindAtlasTexture(regionId);
        if (textureId is null || !_textures.TryGetValue(textureId, out var texture))
        {
            Fill(x, y, width, height, new XnaColor(160, 120, 80));
            _drewSprites = true;
            return;
        }

        var source = new XnaRectangle(region.X, region.Y, region.Width, region.Height);
        _spriteBatch.Draw(texture, new XnaRectangle(x, y, width, height), source, XnaColor.White);
        _drewSprites = true;
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
            regionId.StartsWith("modules/", StringComparison.Ordinal) ||
            regionId.StartsWith("projectiles/", StringComparison.Ordinal))
            return "atlases/player-modules";
        if (regionId.StartsWith("enemies/", StringComparison.Ordinal) ||
            regionId.StartsWith("telegraphs/", StringComparison.Ordinal) ||
            regionId.StartsWith("effects/", StringComparison.Ordinal))
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

    public void Dispose()
    {
        _spriteBatch.Dispose();
        _pixel.Dispose();
    }
}
