using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal interface IWeaponFireStrategy
{
    WeaponBehavior Behavior { get; }

    void Resolve(
        long tick,
        EntityId entity,
        bool firing,
        Vector2 aim,
        WeaponDefinition definition,
        TemporaryCombatModifiers modifiers,
        ref WeaponState state,
        WeaponFireActions actions);
}
