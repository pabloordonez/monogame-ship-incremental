using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class BeamWeaponFireStrategy : IWeaponFireStrategy
{
    public WeaponBehavior Behavior => WeaponBehavior.Beam;

    public void Resolve(
        long tick,
        EntityId entity,
        bool firing,
        Vector2 aim,
        WeaponDefinition definition,
        TemporaryCombatModifiers modifiers,
        ref WeaponState state,
        WeaponFireActions actions)
    {
        if (!firing)
        {
            var idleHeat = MathF.Max(0, state.Heat - definition.CoolPerTick);
            state = state with { Heat = idleHeat, HeatLocked = state.HeatLocked && idleHeat > 0, Target = default };
            return;
        }

        // Overheated: vent even if the trigger is still held so the beam recovers in combat.
        if (state.HeatLocked)
        {
            var ventHeat = MathF.Max(0, state.Heat - definition.CoolPerTick);
            state = state with { Heat = ventHeat, HeatLocked = ventHeat > 0, Target = default };
            return;
        }

        var cone = definition.LockConeDegrees > 0 ? definition.LockConeDegrees : 24f;
        var target = actions.FindTargetInCone(entity, aim, definition.Range, cone);
        if (target != default)
        {
            actions.QueueDamage(
                target,
                entity,
                definition.Damage * FlightCombatConstants.TickSeconds * modifiers.DamageMultiplier * modifiers.FireRateMultiplier,
                false);
            actions.AddEvent(CombatEvent.Create(
                CombatEventKind.WeaponFired,
                tick,
                entity,
                target,
                definition.Id));
        }

        var nextHeat = MathF.Min(180, state.Heat + definition.HeatPerTick);
        state = state with { Heat = nextHeat, HeatLocked = nextHeat >= 180, Target = target };
    }
}
