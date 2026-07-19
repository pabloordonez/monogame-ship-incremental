using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class ConsumeFlightCommandsSystem(FlightCombatContext context) : ISystem
{
    public string Name => "ConsumeFlightCommands";

    public void Update(World world, long tick)
    {
        if (context.Player == default || !context.World.IsAlive(context.Player))
        {
            context.TryTakeCommand(tick, out _);
            return;
        }
        ref var intent = ref context.World.Get<ControlIntent>(context.Player);
        var previous = intent.Actions;
        var command = context.TryTakeCommand(tick, out var found)
            ? found
            : FlightCommandFrame.Neutral(tick);
        var aim = command.Aim;
        if (aim.LengthSquared() <= 0.0001f)
            aim = intent.Aim;
        intent = new ControlIntent(command.Move, aim, command.Actions, previous);
    }
}
