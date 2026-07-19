using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public enum WeaponBehavior : byte
{
    Pulse,
    Beam,
    Seeker
}
