using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class ResolveWeaponsSystem(FlightCombatContext context) : ISystem
{
    public string Name => "ResolveWeapons";

    public void Update(World world, long tick)
    {
        context.RebuildSortedLive();
        var count = context.SortedLive.Count;
        for (var i = 0; i < count; i++)
        {
            var entity = context.SortedLive[i];
            if (!context.Has<PlayerControlled>(entity) || !context.Has<WeaponMount>(entity) || context.Has<Destroyed>(entity))
                continue;
            var intent = context.World.Get<ControlIntent>(entity);
            var firing = (intent.Actions & FlightAction.Fire) != 0;
            ref var state = ref context.World.Get<WeaponState>(entity);
            var definition = context.Registry.Weapon(context.World.Get<WeaponMount>(entity).BehaviorId);
            var modifiers = context.World.Get<TemporaryCombatModifiers>(entity);
            context.WeaponStrategies.Get(definition.Behavior).Resolve(
                tick,
                entity,
                firing,
                intent.Aim,
                definition,
                modifiers,
                ref state,
                context.WeaponFireActions);
        }
    }
}
