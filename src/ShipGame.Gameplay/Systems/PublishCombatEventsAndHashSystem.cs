using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class PublishCombatEventsAndHashSystem(FlightCombatContext context) : ISystem
{
    public string Name => "PublishCombatEventsAndHash";

    public void Update(World world, long tick) =>
        context.LastStateHash = context.CalculateHash();
}
