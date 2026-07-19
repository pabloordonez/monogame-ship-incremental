using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class ApplyFlightCombatStructuralChangesSystem(FlightCombatContext context) : ISystem
{
    public string Name => "ApplyFlightCombatStructuralChanges";

    public void Update(World world, long tick)
    {
        for (var i = 0; i < context.PendingDestroy.Count; i++)
        {
            var entity = context.PendingDestroy[i];
            if (!context.World.IsAlive(entity))
                continue;
            context.World.Destroy(entity);
            if (context.Player == entity)
                context.Player = default;
        }
        context.PendingDestroy.Clear();
    }
}
