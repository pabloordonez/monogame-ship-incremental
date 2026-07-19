using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class LootGenerationSystem(RandomStreams random)
{
    private readonly Pcg32 _loot = random?.Get(RngStream.Loot) ?? throw new ArgumentNullException(nameof(random));
    private bool _eliteDataCoreSpawned;

    public IReadOnlyList<LootSpawnedFact> Spawn(
        World world,
        IEnumerable<CellBrokenFact> brokenCells,
        bool fractureLens = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(brokenCells);
        var facts = brokenCells.Take(FieldDescriptor.MaximumAsteroidCells + 1).OrderBy(fact => fact.CellId).ToArray();
        if (facts.Length > FieldDescriptor.MaximumAsteroidCells)
            throw new ArgumentException("Broken-cell limit exceeded.", nameof(brokenCells));
        var spawned = new List<LootSpawnedFact>();
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
            var pickup = world.Create();
            var sourcePosition = world.Store<WorldPosition>().Has(broken.Cell)
                ? world.Store<WorldPosition>().Read(broken.Cell)
                : default;
            _ = new ResourcePickup(world, pickup, sourcePosition, resource, quantity);
            spawned.Add(new(pickup, resource, quantity));
        }
        return spawned;
    }

    public LootSpawnedFact? SpawnEliteDataCore(World world, WorldPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (_eliteDataCoreSpawned)
            return null;
        _eliteDataCoreSpawned = true;
        var pickup = world.Create();
        _ = new ResourcePickup(world, pickup, position, WorldRunIds.DataCore, 1);
        return new(pickup, WorldRunIds.DataCore, 1);
    }
}
