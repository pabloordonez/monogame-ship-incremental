using ShipGame.Domain;
using ShipGame.Persistence;
using ShipGame.Gameplay;
using ShipGame.Telemetry;

namespace ShipGame.Game;

public sealed record StationView(
    ResourceAmounts Balances,
    LifetimeCounters Counters,
    DerivedShipStatistics Statistics,
    RunSummarySnapshot? PreviousRun,
    IReadOnlyList<LoadoutDiagnostic> LoadoutDiagnostics);
