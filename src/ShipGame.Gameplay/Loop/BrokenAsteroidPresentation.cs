using System.Numerics;

namespace ShipGame.Gameplay;

/// <summary>World-space asteroid break for presentation VFX.</summary>
public readonly record struct BrokenAsteroidPresentation(Vector2 Position, AsteroidCellKind Kind);