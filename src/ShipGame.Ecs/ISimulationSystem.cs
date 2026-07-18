namespace ShipGame.Ecs;

public interface ISimulationSystem
{
    string Name { get; }
    void Update(World world, long tick);
}
