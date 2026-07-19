using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct ComposedPickupView(int X, int Y, ContentId ResourceId, int Quantity);
