namespace ShipGame.Simulation;

internal sealed class HazardDamageWorldEventHandler : IWorldRunEventHandler
{
    public WorldRunEventKind Kind => WorldRunEventKind.HazardDamageRequested;

    public void Handle(in WorldRunEvent worldEvent, IWorldRunEventHost host)
    {
        if (host.PlayerEntity == default)
            return;
        host.InflictDamage(
            host.PlayerEntity,
            host.PlayerEntity,
            Math.Max(1, worldEvent.Amount),
            projectile: false);
    }
}
