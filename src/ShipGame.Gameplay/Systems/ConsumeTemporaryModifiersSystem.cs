using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class ConsumeTemporaryModifiersSystem(FlightCombatContext context) : ISystem
{
    public string Name => "ConsumeTemporaryModifiers";

    public void Update(World world, long tick)
    {
        if (context.Player == default || !context.Has<PendingTemporaryModifier>(context.Player))
            return;
        var grant = context.World.Get<PendingTemporaryModifier>(context.Player).Value;
        context.World.Get<TemporaryCombatModifiers>(context.Player) = grant;
        context.World.Remove<PendingTemporaryModifier>(context.Player);
    }
}
