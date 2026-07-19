using ShipGame.Domain;

namespace ShipGame.Gameplay;

internal sealed class EliteActivationWorldEventHandler : IWorldRunEventHandler
{
    public WorldRunEventKind Kind => WorldRunEventKind.EliteActivationRequested;

    public void Handle(in WorldRunEvent worldEvent, IWorldRunEventHost host)
    {
        if (!host.TrySpawnEliteEnemy(new ContentId("ENM_GUNSHIP"), host.EliteArenaWorldCenter, out _))
            return;
        host.NoteCheckpoint("elite_spawned");
    }
}
