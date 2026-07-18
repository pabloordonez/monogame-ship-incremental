using ShipGame.Simulation;

namespace ShipGame.Game;

public sealed class MetaDrawContext
{
    public required MetaSession Session { get; init; }
    public required ComposedRunOrchestrator? Run { get; init; }
    public required UiShell Ui { get; init; }
    public required RunPresentationHints Hints { get; init; }
    public required IMetaScreenCanvas Canvas { get; init; }
}
