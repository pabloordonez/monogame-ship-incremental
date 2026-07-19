using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class LootGenerationSystem(RandomStreams random)
{
    public const int PickupGraceTicks = 18;
    public const int ScatterMin = 36;
    public const int ScatterMax = 88;

    private readonly Pcg32 _loot = random?.Get(RngStream.Loot) ?? throw new ArgumentNullException(nameof(random));
    private bool _eliteDataCoreSpawned;

    public IReadOnlyList<LootSpawnedFact> Spawn(
        World world,
        IEnumerable<CellBrokenFact> brokenCells,
        long currentTick,
        bool fractureLens = false,
        decimal ferriteYieldMultiplier = 1m,
        bool richEnvironmentYield = false)
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
            var sourcePosition = world.Store<WorldPosition>().Has(broken.Cell)
                ? world.Store<WorldPosition>().Read(broken.Cell)
                : default;

            if (broken.Kind == AsteroidCellKind.Ordinary)
            {
                // Ordinary rock scrap: often one ferrite chip so the field feels alive.
                if (EncounterGenerator.NextInt(_loot, 0, 100) < 55)
                    spawned.Add(SpawnOne(world, sourcePosition, WorldRunIds.Ferrite, 1, afterTick));
                continue;
            }

            if (broken.Kind == AsteroidCellKind.Lumen)
            {
                // Cinder: NextInt(1,3) → 1–2; Ion rich: NextInt(2,5) → 2–4.
                var lumenCount = richEnvironmentYield
                    ? EncounterGenerator.NextInt(_loot, 2, 5)
                    : EncounterGenerator.NextInt(_loot, 1, 3);
                for (var i = 0; i < lumenCount; i++)
                    spawned.Add(SpawnOne(world, sourcePosition, WorldRunIds.Lumen, 1, afterTick));
                continue;
            }

            // Ferrite vein: several visible gems. Ion Veil veins roll +1 quantity.
            var quantity = EncounterGenerator.NextInt(_loot, 3, 7);
            if (richEnvironmentYield)
                quantity += 1;
            if (fractureLens)
                quantity = (quantity * 120 + 99) / 100;
            if (ferriteYieldMultiplier > 1m)
                quantity = (int)Math.Ceiling(quantity * ferriteYieldMultiplier);
            var gemCount = Math.Clamp(quantity, 2, 6);
            var perGem = Math.Max(1, quantity / gemCount);
            var remainder = quantity - perGem * gemCount;
            for (var i = 0; i < gemCount; i++)
            {
                var amount = perGem + (i < remainder ? 1 : 0);
                spawned.Add(SpawnOne(world, sourcePosition, WorldRunIds.Ferrite, amount, afterTick));
            }
        }
        return spawned;
    }

    public IReadOnlyList<LootSpawnedFact> SpawnSalvageBurst(
        World world,
        WorldPosition position,
        long currentTick,
        int ferriteTotal,
        bool chanceLumen = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        var spawned = new List<LootSpawnedFact>();
        var afterTick = currentTick + PickupGraceTicks;
        var gems = Math.Clamp((ferriteTotal + 1) / 2, 2, 5);
        var per = Math.Max(1, ferriteTotal / gems);
        var rem = ferriteTotal - per * gems;
        for (var i = 0; i < gems; i++)
        {
            var amount = per + (i < rem ? 1 : 0);
            if (amount <= 0)
                continue;
            spawned.Add(SpawnOne(world, position, WorldRunIds.Ferrite, amount, afterTick));
        }

        if (chanceLumen && EncounterGenerator.NextInt(_loot, 0, 100) < 28)
            spawned.Add(SpawnOne(world, position, WorldRunIds.Lumen, 1, afterTick));

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
        return SpawnOne(world, position, resourceId, quantity, currentTick + PickupGraceTicks);
    }

    public LootSpawnedFact? SpawnEliteDataCore(World world, WorldPosition position, long currentTick)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (_eliteDataCoreSpawned)
            return null;
        _eliteDataCoreSpawned = true;
        return SpawnOne(world, position, WorldRunIds.DataCore, 1, currentTick + PickupGraceTicks);
    }

    private LootSpawnedFact SpawnOne(
        World world,
        WorldPosition origin,
        ContentId resourceId,
        int quantity,
        long collectibleAfterTick)
    {
        var pickup = world.Create();
        // Spawn at the break center; CollectionSystem applies outward burst then tractor pull.
        _ = new ResourcePickup(world, pickup, origin, resourceId, quantity, collectibleAfterTick);
        var angle = EncounterGenerator.NextInt(_loot, 0, 360) * (MathF.PI / 180f);
        var speed = EncounterGenerator.NextInt(_loot, ScatterMin / 4, ScatterMax / 3 + 1);
        world.Set(pickup, new PickupBurst
        {
            VelocityX = (int)MathF.Round(MathF.Cos(angle) * speed),
            VelocityY = (int)MathF.Round(MathF.Sin(angle) * speed),
            RemainingTicks = PickupGraceTicks + 12
        });
        return new(pickup, resourceId, quantity);
    }
}
