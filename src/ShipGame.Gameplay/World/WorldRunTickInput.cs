using ShipGame.Domain;

namespace ShipGame.Gameplay;

public readonly record struct WorldRunTickInput(
    bool Paused = false,
    bool PlayerHullDepleted = false,
    bool PlayerInExtractionZone = false,
    bool InteractHeld = false,
    GridPoint PlayerCell = default,
    bool BehindLargeAsteroid = false,
    int? UpgradeChoiceIndex = null,
    IReadOnlyList<RunFact>? Facts = null);
