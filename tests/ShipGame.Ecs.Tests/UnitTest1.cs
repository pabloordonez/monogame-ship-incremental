namespace ShipGame.Ecs.Tests;

public class EcsTests
{
    private readonly record struct Position(int Value);
    private readonly record struct Velocity(int Value);

    [Fact]
    public void ReusedIndexInvalidatesStaleEntity()
    {
        var world = new World();
        var first = world.Create();
        world.Set(first, new Position(1));
        world.Destroy(first);
        var second = world.Create();

        Assert.Equal(first.Index, second.Index);
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.False(world.IsAlive(first));
        Assert.Throws<InvalidOperationException>(() => world.Get<Position>(first));
    }

    [Fact]
    public void SparseSetSwapRemovalPreservesRemainingComponent()
    {
        var world = new World();
        var first = world.Create();
        var second = world.Create();
        world.Set(first, new Position(10));
        world.Set(second, new Position(20));

        Assert.True(world.Remove<Position>(first));
        Assert.Equal(20, world.Get<Position>(second).Value);
    }

    [Fact]
    public void QueriesAreStableAcrossInsertionOrders()
    {
        static EntityId[] Build(bool reverse)
        {
            var world = new World();
            var entities = Enumerable.Range(0, 4).Select(_ => world.Create()).ToArray() ?? [];

            if (reverse) 
                entities.Reverse();

            foreach (var entity in entities)
            {
                world.Set(entity, new Position(entity.Index));
                if (entity.Index % 2 == 0)
                    world.Set(entity, new Velocity(entity.Index));
            }
            return world.Query<Position, Velocity>().ToArray();
        }

        Assert.Equal(Build(false), Build(true));
    }

    [Fact]
    public void StructuralChangesApplyAtSynchronizationPoint()
    {
        var world = new World();
        var buffer = new CommandBuffer();
        EntityId created = default;
        buffer.Enqueue(value =>
        {
            created = value.Create();
            value.Set(created, new Position(3));
        });

        Assert.False(world.IsAlive(created));
        buffer.Apply(world);
        Assert.Equal(3, world.Get<Position>(created).Value);
    }

    [Fact]
    public void ComponentViewsExposeNoRawMutationAndStaleIdsAreRejected()
    {
        var world = new World();
        var stale = world.Create();
        world.Destroy(stale);

        Assert.DoesNotContain(
            typeof(IComponentView<Position>).GetMethods(),
            method => method.Name is "Set" or "Remove");
        Assert.Throws<InvalidOperationException>(() => world.Set(stale, new Position(1)));
    }

    [Fact]
    public void EntitySnapshotsCannotMutateSparseDenseStorage()
    {
        var world = new World();
        var first = world.Create();
        var second = world.Create();
        world.Set(first, new Position(10));
        world.Set(second, new Position(20));
        var snapshot = world.Store<Position>().Entities;

        Assert.False(snapshot is IList<EntityId>);
        Assert.False(snapshot is System.Collections.IList);

        Assert.True(world.Remove<Position>(first));
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(1, world.Store<Position>().Count);
        Assert.False(world.Store<Position>().Has(first));
        Assert.True(world.Store<Position>().Has(second));
        Assert.Equal(20, world.Store<Position>().Read(second).Value);
        Assert.Equal(20, world.Get<Position>(second).Value);
    }

    [Fact]
    public void StructuralMutationDuringQueryMustBeBuffered()
    {
        var world = new World();
        var entity = world.Create();
        world.Set(entity, new Position(1));
        world.Set(entity, new Velocity(1));

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var current in world.Query<Position, Velocity>())
                world.Remove<Velocity>(current);
        });
        Assert.True(world.Store<Velocity>().Has(entity));

        var buffer = new CommandBuffer();
        foreach (var current in world.Query<Position, Velocity>())
            buffer.Enqueue(value => value.Remove<Velocity>(current));
        buffer.Apply(world);
        Assert.False(world.Store<Velocity>().Has(entity));
    }

    [Fact]
    public void RandomizedReuseNeverCreatesOrphanComponents()
    {
        var random = new Random(1729);
        var world = new World();
        var live = new List<EntityId>();
        for (var step = 0; step < 1000; step++)
        {
            if (live.Count == 0 || random.Next(3) != 0)
            {
                var entity = world.Create();
                live.Add(entity);
                if (random.Next(2) == 0)
                    world.Set(entity, new Position(step));
            }
            else
            {
                var index = random.Next(live.Count);
                var entity = live[index];
                world.Destroy(entity);
                live.RemoveAt(index);
                Assert.False(world.Store<Position>().Has(entity));
            }
        }

        Assert.All(world.Store<Position>().Entities, entity => Assert.True(world.IsAlive(entity)));
        Assert.Equal(world.Store<Position>().Entities.Distinct().Count(), world.Store<Position>().Count);
    }

    [Fact]
    public void ThrowingStructuralCommandIsConsumedWithoutReplayingCompletedCommands()
    {
        var world = new World();
        var buffer = new CommandBuffer();
        var applied = 0;
        buffer.Enqueue(_ => applied++);
        buffer.Enqueue(_ => throw new InvalidOperationException("expected"));
        buffer.Enqueue(_ => applied += 10);

        Assert.Throws<InvalidOperationException>(() => buffer.Apply(world));
        buffer.Apply(world);

        Assert.Equal(11, applied);
    }

    [Fact]
    public void SchedulerRejectsDuplicateNamesAndKeepsExplicitOrder()
    {
        var scheduler = new SystemScheduler();
        scheduler.Add(new NamedSystem("first"));
        scheduler.Add(new NamedSystem("second"));

        Assert.Equal(["first", "second"], scheduler.Order);
        Assert.Throws<InvalidOperationException>(() => scheduler.Add(new NamedSystem("first")));
    }

    private sealed class NamedSystem(string name) : ISystem
    {
        public string Name => name;
        public void Update(World world, long tick) { }
    }
}
