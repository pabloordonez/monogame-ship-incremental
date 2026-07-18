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

        Assert.True(world.Store<Position>().Remove(first));
        Assert.Equal(20, world.Get<Position>(second).Value);
    }

    [Fact]
    public void QueriesAreStableAcrossInsertionOrders()
    {
        static EntityId[] Build(bool reverse)
        {
            var world = new World();
            var entities = Enumerable.Range(0, 4).Select(_ => world.Create()).ToArray();
            foreach (var entity in reverse ? entities.Reverse() : entities)
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
    public void SchedulerRejectsDuplicateNamesAndKeepsExplicitOrder()
    {
        var scheduler = new SystemScheduler();
        scheduler.Add(new NamedSystem("first"));
        scheduler.Add(new NamedSystem("second"));

        Assert.Equal(["first", "second"], scheduler.Order);
        Assert.Throws<InvalidOperationException>(() => scheduler.Add(new NamedSystem("first")));
    }

    private sealed class NamedSystem(string name) : ISimulationSystem
    {
        public string Name => name;
        public void Update(World world, long tick) { }
    }
}
