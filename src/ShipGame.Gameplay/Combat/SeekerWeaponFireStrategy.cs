using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

internal sealed class SeekerWeaponFireStrategy : IWeaponFireStrategy
{
    public WeaponBehavior Behavior => WeaponBehavior.Seeker;

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
        var lockTarget = actions.FindTargetInCone(entity, aim, definition.Range, definition.LockConeDegrees);
        if (lockTarget == default)
            return;
        actions.SpawnPlayerProjectiles(new PlayerProjectileSpawnRequest(
            entity,
            aim,
            definition,
            modifiers,
            lockTarget,
            Homing: true,
            definition.TurnDegreesPerSecond));
        state = state with { CooldownTicks = cadence, Target = lockTarget };
    }
}
