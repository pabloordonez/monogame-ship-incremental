using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

internal sealed class PulseWeaponFireStrategy : IWeaponFireStrategy
{
    public WeaponBehavior Behavior => WeaponBehavior.Pulse;

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
        if (!firing || state.CooldownTicks > 0)
            return;
        var cadence = Math.Max(1, (int)MathF.Round(definition.CadenceTicks / modifiers.FireRateMultiplier));
        actions.SpawnPlayerProjectiles(new PlayerProjectileSpawnRequest(
            entity,
            aim,
            definition,
            modifiers,
            Target: default,
            Homing: false,
            TurnDegreesPerSecond: 0));
        state = state with { CooldownTicks = cadence };
    }
}
