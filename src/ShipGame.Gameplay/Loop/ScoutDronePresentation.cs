using System.Numerics;

namespace ShipGame.Gameplay;

public readonly record struct ScoutDronePresentation(
    bool Active,
    Vector2 WorldPosition,
    float Rotation);
