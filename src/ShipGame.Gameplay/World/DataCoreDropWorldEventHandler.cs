namespace ShipGame.Gameplay;

internal sealed class DataCoreDropWorldEventHandler : IWorldRunEventHandler
{
    public WorldRunEventKind Kind => WorldRunEventKind.DataCoreDropRequested;

    public void Handle(in WorldRunEvent worldEvent, IWorldRunEventHost host)
    {
        var player = host.PlayerWorldPosition;
        host.SpawnEliteDataCore(new WorldPosition
        {
            X = (int)MathF.Round(player.X),
            Y = (int)MathF.Round(player.Y)
        });
    }
}
