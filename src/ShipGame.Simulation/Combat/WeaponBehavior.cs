using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public enum WeaponBehavior : byte
{
    Pulse,
    Beam,
    Seeker
}
