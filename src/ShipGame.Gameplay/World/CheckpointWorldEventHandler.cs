namespace ShipGame.Gameplay;

internal sealed class CheckpointWorldEventHandler(WorldRunEventKind kind, string checkpoint) : IWorldRunEventHandler
{
    public WorldRunEventKind Kind { get; } = kind;

    public void Handle(in WorldRunEvent worldEvent, IWorldRunEventHost host) =>
        host.NoteCheckpoint(checkpoint);
}
