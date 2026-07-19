using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct SpawnAnchor(Vector2 Position, bool OutsideCamera);
