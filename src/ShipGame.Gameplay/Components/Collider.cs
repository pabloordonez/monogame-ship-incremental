using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct Collider(float Radius, uint Layer, uint Mask, bool BlocksMovement = true);
