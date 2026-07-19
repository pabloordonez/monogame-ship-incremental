namespace ShipGame.Gameplay;

internal sealed class DataCoreDropWorldEventHandler : IWorldRunEventHandler
{
    public WorldRunEventKind Kind => WorldRunEventKind.DataCoreDropRequested;

    public void Handle(in WorldRunEvent worldEvent, IWorldRunEventHost host)
    {
        var death = host.LastEliteDeathWorldPosition;
        var position = death == default ? host.PlayerWorldPosition : death;
        host.SpawnEliteDataCore(new WorldPosition
        {
            X = (int)MathF.Round(position.X),
            Y = (int)MathF.Round(position.Y)
        });
    }
}
