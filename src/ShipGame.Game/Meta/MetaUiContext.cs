using ShipGame.Domain;
using ShipGame.Gameplay;

namespace ShipGame.Game;

public sealed class MetaUiContext
{
    public required UiShell Ui { get; init; }
    public required MetaSession Session { get; init; }
    public ComposedRunOrchestrator? Run { get; set; }
    public required Action ExitGame { get; init; }
    public required Action StartRun { get; init; }
    public required Action ClearRun { get; init; }
    public required Action CommitRunReward { get; init; }
    public required Func<string, string> NextTransactionId { get; init; }
    public required Action<GameSettings> ApplySettings { get; init; }
    public required Func<ModuleSlot, string> EffectiveModule { get; init; }
    public bool WindowSmokeHarnessStarted { get; set; }
    public bool WindowSmokeVisitedSummary { get; set; }
    public float DeltaSeconds { get; set; }
    public bool ActivatePressed { get; set; }
    public bool PointerPressed { get; set; }
    public bool WindowSmoke { get; set; }
}
