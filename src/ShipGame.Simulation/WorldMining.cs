using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public struct MineableCell
{
    public int CellId;
    public AsteroidCellKind Kind;
    public int Health;
    public bool Broken;
}

public struct WorldPosition
{
    public int X;
    public int Y;
}

public struct Collectible
{
    public ContentId ResourceId;
    public int Quantity;
    public bool Credited;
}

public struct CollectionRadius
{
    public int Radius;
    public int PullSpeedPerTick;
}

public readonly record struct MiningContact(EntityId Source, EntityId Cell, int Damage);
public readonly record struct CellBrokenFact(EntityId Cell, int CellId, AsteroidCellKind Kind);
public readonly record struct LootSpawnedFact(EntityId Pickup, ContentId ResourceId, int Quantity);
public readonly record struct ResourceCollectedFact(EntityId Pickup, ContentId ResourceId, int Quantity);

public sealed class MiningSystem
{
    public IReadOnlyList<CellBrokenFact> Resolve(World world, IEnumerable<MiningContact> contacts)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(contacts);
        var materialized = contacts.Take(4097).ToArray();
        if (materialized.Length > 4096)
            throw new ArgumentException("Mining contact limit exceeded.", nameof(contacts));
        var broken = new List<CellBrokenFact>();
        foreach (var contact in materialized.OrderBy(contact => contact.Cell).ThenBy(contact => contact.Source))
        {
            if (contact.Damage <= 0 || !world.IsAlive(contact.Cell) || !world.Store<MineableCell>().Has(contact.Cell))
                continue;
            ref var cell = ref world.Get<MineableCell>(contact.Cell);
            if (cell.Broken)
                continue;
            cell.Health = Math.Max(0, cell.Health - Math.Min(contact.Damage, 100_000));
            if (cell.Health != 0)
                continue;
            cell.Broken = true;
            broken.Add(new(contact.Cell, cell.CellId, cell.Kind));
        }
        return broken;
    }
}

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
            world.Set(pickup, sourcePosition);
            world.Set(pickup, new Collectible { ResourceId = resource, Quantity = quantity });
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
        world.Set(pickup, position);
        world.Set(pickup, new Collectible
        {
            ResourceId = WorldRunIds.DataCore,
            Quantity = 1
        });
        return new(pickup, WorldRunIds.DataCore, 1);
    }
}

public sealed class CollectionSystem
{
    public IReadOnlyList<ResourceCollectedFact> Resolve(World world, EntityId collector)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.IsAlive(collector) ||
            !world.Store<WorldPosition>().Has(collector) ||
            !world.Store<CollectionRadius>().Has(collector))
            return Array.Empty<ResourceCollectedFact>();

        var collectorPosition = world.Store<WorldPosition>().Read(collector);
        var collection = world.Store<CollectionRadius>().Read(collector);
        var radius = Math.Clamp(collection.Radius, 0, 10_000);
        var pull = Math.Clamp(collection.PullSpeedPerTick, 0, 1_000);
        var collected = new List<ResourceCollectedFact>();
        var destroy = new List<EntityId>();
        foreach (var pickup in world.Query<Collectible, WorldPosition>())
        {
            ref var item = ref world.Get<Collectible>(pickup);
            if (item.Credited || item.Quantity <= 0)
                continue;
            ref var position = ref world.Get<WorldPosition>(pickup);
            var dx = collectorPosition.X - position.X;
            var dy = collectorPosition.Y - position.Y;
            var distanceSquared = (long)dx * dx + (long)dy * dy;
            if (distanceSquared <= (long)radius * radius)
            {
                item.Credited = true;
                collected.Add(new(pickup, item.ResourceId, item.Quantity));
                destroy.Add(pickup);
                continue;
            }
            if (pull == 0)
                continue;
            position.X += Math.Clamp(dx, -pull, pull);
            position.Y += Math.Clamp(dy, -pull, pull);
        }
        foreach (var pickup in destroy)
            world.Destroy(pickup);
        return collected;
    }
}
