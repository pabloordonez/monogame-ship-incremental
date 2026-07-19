using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Gameplay;

public readonly record struct AsteroidCellDescriptor(
    int CellId,
    GridPoint Position,
    AsteroidCellKind Kind,
    int Health,
    bool ProvidesCompleteCover);
