using ShipGame.Domain;

namespace ShipGame.Simulation;

public readonly record struct WorldRunEvent(
    long Sequence,
    long RunTick,
    WorldRunEventKind Kind,
    ContentId ContentId = default,
    int Amount = 0,
    int SecondaryAmount = 0);
