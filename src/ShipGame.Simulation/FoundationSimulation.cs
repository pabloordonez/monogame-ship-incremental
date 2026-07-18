using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public enum AppState
{
    Title,
    Lobby,
    Run,
    Summary
}

public readonly record struct CommandFrame(
    long TargetTick,
    short MoveX = 0,
    short MoveY = 0,
    short AimX = 0,
    short AimY = 0,
    bool Confirm = false,
    bool Return = false)
{
    public static CommandFrame Neutral(long tick) => new(tick);
}

public sealed class FoundationSimulation
{
    public const int TickRate = 60;
    public const int EmptyRunTicks = 180;

    private readonly SortedDictionary<long, CommandFrame> _commands = [];
    private readonly RandomStreams _random;
    private readonly World _world = new();
    private readonly CommandBuffer _structuralChanges = new();
    private readonly SystemScheduler _scheduler = new();
    private uint _runSignature;

    public FoundationSimulation(ulong seed, long runIndex = 0)
    {
        if (runIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(runIndex));
        Seed = seed;
        RunIndex = runIndex;
        _random = new RandomStreams(DeriveRunSeed(seed, runIndex));
        _scheduler.Add(new DelegateSystem("ApplyStructuralChanges", (_, _) => _structuralChanges.Apply(_world)));
        _scheduler.Add(new DelegateSystem("ConsumeCommands", (_, tick) => Consume(_commands.Remove(tick, out var command) ? command : CommandFrame.Neutral(tick))));
        _scheduler.Add(new DelegateSystem("SessionTransitions", (_, _) => AdvanceSession()));
        _scheduler.Add(new DelegateSystem("RunClock", (_, _) => AdvanceClock()));
        _scheduler.Add(new DelegateSystem("PublishAndHash", (_, _) => LastStateHash = CalculateHash()));
    }

    public ulong Seed { get; }
    public long RunIndex { get; }
    public long Tick { get; private set; }
    public AppState State { get; private set; } = AppState.Title;
    public long RunTick { get; private set; }
    public ulong LastStateHash { get; private set; }
    public uint RunSignature => _runSignature;
    public IReadOnlyList<string> Schedule => _scheduler.Order;

    public bool Queue(CommandFrame command)
    {
        if (command.TargetTick < Tick || command.TargetTick > Tick + TickRate * 10L)
            return false;
        _commands[command.TargetTick] = command;
        return true;
    }

    public ulong Step()
    {
        _scheduler.Tick(_world, Tick);
        Tick++;
        return LastStateHash;
    }

    private bool _confirm;
    private bool _return;

    private void Consume(CommandFrame command)
    {
        _confirm = command.Confirm;
        _return = command.Return;
    }

    private void AdvanceSession()
    {
        if (_confirm && State == AppState.Title)
            State = AppState.Lobby;
        else if (_confirm && State == AppState.Lobby)
        {
            State = AppState.Run;
            RunTick = 0;
            _runSignature = _random.Get(RngStream.Encounter).NextUInt();
        }
        else if ((_confirm || _return) && State == AppState.Summary)
            State = AppState.Lobby;
    }

    private void AdvanceClock()
    {
        if (State != AppState.Run)
            return;
        RunTick++;
        if (RunTick >= EmptyRunTicks)
            State = AppState.Summary;
    }

    private ulong CalculateHash()
    {
        var hash = StableHash.Offset;
        hash = StableHash.Add(hash, Seed);
        hash = StableHash.Add(hash, unchecked((ulong)RunIndex));
        hash = StableHash.Add(hash, (ulong)Tick);
        hash = StableHash.Add(hash, (ulong)State);
        hash = StableHash.Add(hash, (ulong)RunTick);
        hash = StableHash.Add(hash, _runSignature);
        hash = StableHash.Add(hash, _confirm ? 1UL : 0UL);
        hash = StableHash.Add(hash, _return ? 1UL : 0UL);
        hash = StableHash.Add(hash, (ulong)_commands.Count);
        foreach (var (targetTick, command) in _commands)
        {
            hash = StableHash.Add(hash, unchecked((ulong)targetTick));
            hash = StableHash.Add(hash, unchecked((ulong)command.MoveX));
            hash = StableHash.Add(hash, unchecked((ulong)command.MoveY));
            hash = StableHash.Add(hash, unchecked((ulong)command.AimX));
            hash = StableHash.Add(hash, unchecked((ulong)command.AimY));
            hash = StableHash.Add(hash, command.Confirm ? 1UL : 0UL);
            hash = StableHash.Add(hash, command.Return ? 1UL : 0UL);
        }
        return StableHash.Add(hash, _random.CalculateStateHash());
    }

    public static ulong DeriveRunSeed(ulong profileSeed, long runIndex)
    {
        var hash = StableHash.Add(StableHash.Offset, profileSeed);
        hash = StableHash.Add(hash, unchecked((ulong)runIndex));
        return StableHash.Add(hash, ContractVersions.Generation);
    }

    private sealed class DelegateSystem(string name, Action<World, long> update) : ISimulationSystem
    {
        public string Name => name;
        public void Update(World world, long tick) => update(world, tick);
    }
}

public sealed class FixedStepDriver(FoundationSimulation simulation)
{
    public const double TickSeconds = 1d / FoundationSimulation.TickRate;
    public const int MaxCatchUpTicks = 8;
    private double _accumulator;

    public double InterpolationAlpha => _accumulator / TickSeconds;
    public double DroppedSeconds { get; private set; }

    public int Advance(double elapsedSeconds)
    {
        _accumulator += Math.Clamp(elapsedSeconds, 0, 0.25);
        var count = 0;
        while (_accumulator >= TickSeconds && count < MaxCatchUpTicks)
        {
            simulation.Step();
            _accumulator -= TickSeconds;
            count++;
        }
        if (_accumulator >= TickSeconds)
        {
            DroppedSeconds += _accumulator - (_accumulator % TickSeconds);
            _accumulator %= TickSeconds;
        }
        return count;
    }
}
