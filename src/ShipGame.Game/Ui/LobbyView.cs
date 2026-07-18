using ShipGame.Domain;
using ShipGame.Persistence;
using ShipGame.Simulation;
using ShipGame.Telemetry;

namespace ShipGame.Game;

/// <summary>Obsolete alias — prefer <see cref="StationView"/>.</summary>
public sealed record LobbyView(
    ResourceAmounts Balances,
    LifetimeCounters Counters,
    DerivedShipStatistics Statistics,
    RunSummarySnapshot? PreviousRun,
    IReadOnlyList<LoadoutDiagnostic> LoadoutDiagnostics)
{
    public static LobbyView From(StationView view) =>
        new(view.Balances, view.Counters, view.Statistics, view.PreviousRun, view.LoadoutDiagnostics);
}
