using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class LootGenerationSystem(RandomStreams random)
{
    public const int PickupGraceTicks = 24;
    public const int ScatterMin = 48;
    public const int ScatterMax = 96;

    private readonly Pcg32 _loot = random?.Get(RngStream.Loot) ?? throw new ArgumentNullException(nameof(random));
    private bool _eliteDataCoreSpawned;

    public IReadOnlyList<LootSpawnedFact> Spawn(
        World world,
        IEnumerable<CellBrokenFact> brokenCells,
        long currentTick,
        bool fractureLens = false,
        decimal ferriteYieldMultiplier = 1m)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(brokenCells);
        var facts = brokenCells.Take(FieldDescriptor.MaximumAsteroidCells + 1).OrderBy(fact => fact.CellId).ToArray();
        if (facts.Length > FieldDescriptor.MaximumAsteroidCells)
            throw new ArgumentException("Broken-cell limit exceeded.", nameof(brokenCells));
        var spawned = new List<LootSpawnedFact>();
        var afterTick = currentTick + PickupGraceTicks;
        foreach (var broken in facts)
        {
            if (broken.Kind == AsteroidCellKind.Ordinary)
                continue;
            var resource = broken.Kind == AsteroidCellKind.Lumen ? WorldRunIds.Lumen : WorldRunIds.Ferrite;
            var quantity = broken.Kind == AsteroidCellKind.Lumen
                ? 1
                : EncounterGenerator.NextInt(_loot, 2, 5);
            if (fractureLens && resource == WorldRunIds.Ferrite)
                quantity = (quantity * 120 + 99) / 100;
            if (resource == WorldRunIds.Ferrite && ferriteYieldMultiplier > 1m)
                quantity = (int)Math.Ceiling(quantity * ferriteYieldMultiplier);
            var sourcePosition = world.Store<WorldPosition>().Has(broken.Cell)
                ? world.Store<WorldPosition>().Read(broken.Cell)
                : default;
            var pickup = world.Create();
            _ = new ResourcePickup(world, pickup, Scatter(sourcePosition), resource, quantity, afterTick);
            spawned.Add(new(pickup, resource, quantity));
        }
        return spawned;
    }

    public LootSpawnedFact? SpawnSalvage(
        World world,
        WorldPosition position,
        ContentId resourceId,
        int quantity,
        long currentTick)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (quantity <= 0)
            return null;
        var pickup = world.Create();
        _ = new ResourcePickup(
            world,
            pickup,
            Scatter(position),
            resourceId,
            quantity,
            currentTick + PickupGraceTicks);
        return new(pickup, resourceId, quantity);
    }

    public LootSpawnedFact? SpawnEliteDataCore(World world, WorldPosition position, long currentTick)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (_eliteDataCoreSpawned)
            return null;
        _eliteDataCoreSpawned = true;
        var pickup = world.Create();
        _ = new ResourcePickup(
            world,
            pickup,
            Scatter(position),
            WorldRunIds.DataCore,
            1,
            currentTick + PickupGraceTicks);
        return new(pickup, WorldRunIds.DataCore, 1);
    }

    private WorldPosition Scatter(WorldPosition origin)
    {
        var angle = EncounterGenerator.NextInt(_loot, 0, 360) * (MathF.PI / 180f);
        var distance = EncounterGenerator.NextInt(_loot, ScatterMin, ScatterMax + 1);
        return new WorldPosition
        {
            X = origin.X + (int)MathF.Round(MathF.Cos(angle) * distance),
            Y = origin.Y + (int)MathF.Round(MathF.Sin(angle) * distance)
        };
    }
}
