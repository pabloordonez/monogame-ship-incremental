using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct ComposedAsteroidView(
    int CellId,
    int X,
    int Y,
    AsteroidCellKind Kind,
    bool Broken,
    AsteroidCellSize Size,
    float Health,
    float MaxHealth);
