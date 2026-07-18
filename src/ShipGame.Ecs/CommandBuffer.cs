namespace ShipGame.Ecs;

public sealed class CommandBuffer
{
    private readonly List<Action<World>> _commands = [];
    private bool _applying;

    public void Enqueue(Action<World> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_applying)
            throw new InvalidOperationException("Cannot enqueue structural changes while applying the buffer.");
        _commands.Add(command);
    }

    public void Apply(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (_applying)
            throw new InvalidOperationException("The command buffer is already applying.");
        _applying = true;
        try
        {
            while (_commands.Count > 0)
            {
                var command = _commands[0];
                _commands.RemoveAt(0);
                command(world);
            }
        }
        finally
        {
            _applying = false;
        }
    }
}
