using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class ResolveMobilitySystem(FlightCombatContext context) : ISystem
{
    public string Name => "ResolveMobility";

    public void Update(World world, long tick)
    {
        if (context.Player == default || !context.Has<MobilityAbility>(context.Player) || context.Has<Destroyed>(context.Player))
            return;
        var intent = context.World.Get<ControlIntent>(context.Player);
        if ((intent.Actions & FlightAction.Mobility) == 0 ||
            (intent.PreviousActions & FlightAction.Mobility) != 0)
            return;
        ref var ability = ref context.World.Get<MobilityAbility>(context.Player);
        if (ability.CooldownRemaining > 0)
        {
            context.AddEvent(CombatEvent.Create(
                CombatEventKind.AbilityRejected,
                tick,
                context.Player,
                contentId: ability.BehaviorId,
                detail: "cooldown"));
            return;
        }
        var direction = FlightCombatContext.NormalizeOr(intent.Move, FlightCombatContext.NormalizeOr(intent.Aim, Vector2.UnitX));
        var start = context.World.Get<Transform2>(context.Player).Position;
        var destination = context.ShortenAgainstObstacles(context.Player, start, start + direction * ability.Distance);
        ref var transform = ref context.World.Get<Transform2>(context.Player);
        transform = transform with { Position = destination, Rotation = MathF.Atan2(direction.Y, direction.X) };
        var modifiers = context.World.Get<TemporaryCombatModifiers>(context.Player);
        var cooldown = Math.Max(1, (int)MathF.Round(ability.CooldownTicks * modifiers.MobilityCooldownMultiplier));
        ability = ability with
        {
            CooldownRemaining = cooldown,
            ActiveTicksRemaining = ability.Behavior == MobilityBehavior.Dash ? ability.DurationTicks : 0,
            Direction = direction
        };
        if (ability.Behavior == MobilityBehavior.Dash)
            context.World.Set(context.Player, new Invulnerability(ability.DurationTicks));
        context.AddEvent(CombatEvent.Create(
            CombatEventKind.AbilityActivated,
            tick,
            context.Player,
            contentId: ability.BehaviorId,
            position: destination));
        if (modifiers.ShockTransit)
            context.QueueAreaDamage(context.Player, destination, 90, 20 * modifiers.DamageMultiplier, Faction.Hostile);
    }
}
