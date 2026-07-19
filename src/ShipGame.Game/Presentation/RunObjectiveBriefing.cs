using ShipGame.Gameplay;

namespace ShipGame.Game;

public readonly record struct RunObjectiveCopy(string Title, string Body, string Controls);

public static class RunObjectiveBriefing
{
    public static RunObjectiveCopy For(ComposedRunHud hud) =>
        hud.Phase switch
        {
            RunPhase.Objective => new(
                "Field objective",
                $"Mine ferrite asteroids and destroy hostiles. Progress: ferrite {hud.ObjectiveFerrite}/30, kills {hud.ObjectiveKills}/8.",
                "WASD thrust  mouse aim  LMB fire  RMB mine  Space dash"),
            RunPhase.Elite => new(
                "Elite threat",
                "A marked gunship has entered the field. Destroy it to open extraction.",
                "WASD thrust  mouse aim  LMB fire  RMB mine  Space dash"),
            RunPhase.Extraction => new(
                "Extract",
                "Reach the extract gate and hold E for 6s to bank what you are carrying.",
                "WASD thrust  mouse aim  LMB fire  RMB mine  Space dash  E extract"),
            RunPhase.Succeeded => new(
                "Run complete",
                "Extraction succeeded. Banking held resources.",
                string.Empty),
            RunPhase.Failed => new(
                "Run failed",
                "Hull lost or deadline reached. Lumen and cores are not banked on failure.",
                string.Empty),
            _ => new(hud.Phase.ToString(), string.Empty, string.Empty)
        };

    public static string? ToastFor(WorldRunEventKind kind) =>
        kind switch
        {
            WorldRunEventKind.ObjectiveCompleted => "Objective complete — elite gunship inbound",
            WorldRunEventKind.EliteActivationRequested => "Elite marked — destroy it to open extraction",
            WorldRunEventKind.ExtractionActivated => "Extraction open — hold E at the gate",
            _ => null
        };
}
