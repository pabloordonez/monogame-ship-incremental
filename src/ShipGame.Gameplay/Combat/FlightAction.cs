using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

[Flags]
public enum FlightAction : byte
{
    None = 0,
    Fire = 1,
    Mine = 2,
    Mobility = 4,
    Interact = 8
}
