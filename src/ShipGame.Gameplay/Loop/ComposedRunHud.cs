using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct ComposedRunHud(
    long RunTick,
    RunPhase Phase,
    float Hull,
    float Shield,
    int FerriteHeld,
    int LumenHeld,
    int DataCoresHeld,
    int ObjectiveFerrite,
    int ObjectiveKills,
    int ExtractionProgressTicks,
    int ExtractionHoldTicks,
    int ThreatCap);
