using ShipGame.Simulation;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace ShipGame.Game;

/// <summary>Narrow drawing surface for meta screen handlers (MonoGame stays in Game).</summary>
public interface IMetaScreenCanvas
{
    void DrawText(int x, int y, string text, XnaColor color, int scale = 1);
    void DrawRegion(string regionId, int x, int y, int width, int height);
    void DrawRegionRotated(string regionId, XnaVector2 center, float rotationRadians, int size);
    void DrawTexture(string assetId, int x, int y, int width, int height, XnaColor color);
    void DrawShellButtons(UiShell ui, string? skipPrefix = null);
    void DrawButton(UiRect bounds, string label, UiControlState state);
    void DrawBankedPurse(MetaSession session);
    void DrawPanel(string title, params string[] lines);
    void DrawParallaxBackground(string bgId, System.Numerics.Vector2 camera);
    void DrawThrustTrail(XnaVector2 shipCenter, System.Numerics.Vector2 move, long tick);
    void DrawMuzzleFlash(XnaVector2 shipCenter, System.Numerics.Vector2 aim, long tick);
    void DrawMineRay(XnaVector2 shipCenter, System.Numerics.Vector2 aim);
    void DrawAimReticle(System.Numerics.Vector2 mouseVirtual);
    void DrawRunHud(ComposedRunHud hud, RunPresentationHints hints, XnaVector2 playerScreen);
    void UpdateCombatFlash(ComposedRunOrchestrator run, RunPresentationHints hints);
    void DrawRunFlashOverlay(RunPresentationHints hints);
    void Fill(int x, int y, int width, int height, XnaColor color);
    void Frame(int x, int y, int width, int height, XnaColor color, int thickness);
    UiControl? FindControl(UiShell ui, string id);
    string Truncate(string value, int max);
    XnaVector2 WorldToScreen(System.Numerics.Vector2 world, System.Numerics.Vector2 camera);
    bool OnScreen(XnaVector2 screen, int margin);
}
