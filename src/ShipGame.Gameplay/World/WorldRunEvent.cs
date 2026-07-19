using ShipGame.Domain;

namespace ShipGame.Gameplay;

public readonly record struct WorldRunEvent(
    long Sequence,
    long RunTick,
    WorldRunEventKind Kind,
    ContentId ContentId = default,
    int Amount = 0,
    int SecondaryAmount = 0);
