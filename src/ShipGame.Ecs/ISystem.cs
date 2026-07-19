namespace ShipGame.Ecs;

public interface ISystem
{
    string Name { get; }
    void Update(World world, long tick);
}
