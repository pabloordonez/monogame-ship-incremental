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
        var tickDamage = definition.Damage * FlightCombatConstants.TickSeconds *
                         modifiers.DamageMultiplier * modifiers.FireRateMultiplier;
        var maxTargets = 1 + Math.Max(0, modifiers.PierceCount);
        var targets = actions.FindTargetsInConeOrdered(entity, aim, definition.Range, cone, maxTargets);
        EntityId primary = default;
        if (targets.Count > 0)
        {
            primary = targets[0];
            for (var i = 0; i < targets.Count; i++)
                actions.QueueDamage(targets[i], entity, tickDamage, false);

            if (modifiers.ExtraProjectiles > 0)
            {
                var forkAim = FlightCombatContext.Rotate(
                    FlightCombatContext.NormalizeOr(aim, Vector2.UnitX),
                    0.12f);
                var forkTargets = actions.FindTargetsInConeOrdered(
                    entity,
                    forkAim,
                    definition.Range,
                    cone,
                    maxTargets);
                for (var i = 0; i < forkTargets.Count; i++)
                    actions.QueueDamage(forkTargets[i], entity, tickDamage * 0.45f, false);
            }

            actions.AddEvent(CombatEvent.Create(
                CombatEventKind.WeaponFired,
                tick,
                entity,
                primary,
                definition.Id));
        }

        var nextHeat = MathF.Min(180, state.Heat + definition.HeatPerTick);
        state = state with { Heat = nextHeat, HeatLocked = nextHeat >= 180, Target = primary };
    }
}
