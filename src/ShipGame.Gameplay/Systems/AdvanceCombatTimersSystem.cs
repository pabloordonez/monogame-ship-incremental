using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class AdvanceCombatTimersSystem(FlightCombatContext context) : ISystem
{
    public string Name => "AdvanceCombatTimers";

    public void Update(World world, long tick)
    {
        context.RebuildSortedLive();
        for (var i = 0; i < context.SortedLive.Count; i++)
        {
            var entity = context.SortedLive[i];
            if (context.Has<Destroyed>(entity))
                continue;
            if (context.Has<Shield>(entity))
            {
                ref var shield = ref context.World.Get<Shield>(entity);
                var ticks = shield.TicksSinceDamage + 1;
                var current = shield.Current;
                if (ticks >= shield.RechargeDelayTicks && current < shield.Maximum)
                    current = MathF.Min(shield.Maximum, current + shield.RechargePerSecond * FlightCombatConstants.TickSeconds);
                shield = shield with { Current = current, TicksSinceDamage = ticks };
            }
            if (context.Has<WeaponState>(entity))
            {
                ref var state = ref context.World.Get<WeaponState>(entity);
                state = state with { CooldownTicks = Math.Max(0, state.CooldownTicks - 1) };
            }
            if (context.Has<MobilityAbility>(entity))
            {
                ref var ability = ref context.World.Get<MobilityAbility>(entity);
                ability = ability with
                {
                    CooldownRemaining = Math.Max(0, ability.CooldownRemaining - 1),
                    ActiveTicksRemaining = Math.Max(0, ability.ActiveTicksRemaining - 1)
                };
            }
            if (context.Has<Invulnerability>(entity))
            {
                ref var value = ref context.World.Get<Invulnerability>(entity);
                value = new Invulnerability(Math.Max(0, value.TicksRemaining - 1));
            }
            if (context.Has<Projectile>(entity))
            {
                ref var projectile = ref context.World.Get<Projectile>(entity);
                projectile = projectile with { LifetimeTicks = projectile.LifetimeTicks - 1 };
                if (projectile.LifetimeTicks <= 0)
                    context.MarkDestroyed(entity, default);
            }
            if (context.Has<Mine>(entity))
            {
                ref var mine = ref context.World.Get<Mine>(entity);
                mine = mine with
                {
                    ArmTicks = Math.Max(0, mine.ArmTicks - 1),
                    LifetimeTicks = mine.LifetimeTicks - 1
                };
                if (mine.LifetimeTicks <= 0)
                    context.MarkDestroyed(entity, default);
            }
        }
    }
}
