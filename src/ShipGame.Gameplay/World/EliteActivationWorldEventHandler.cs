using ShipGame.Domain;

namespace ShipGame.Gameplay;

internal sealed class EliteActivationWorldEventHandler : IWorldRunEventHandler
{
    public WorldRunEventKind Kind => WorldRunEventKind.EliteActivationRequested;

    public void Handle(in WorldRunEvent worldEvent, IWorldRunEventHost host)
    {
        var spawned = 0;
        while (host.TrySpawnEliteEnemy(new ContentId("ENM_GUNSHIP"), host.EliteArenaWorldCenter, out _))
            spawned++;
        if (spawned > 0)
            host.NoteCheckpoint("elite_spawned");
    }
}
