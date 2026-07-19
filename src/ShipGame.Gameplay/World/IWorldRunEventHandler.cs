namespace ShipGame.Gameplay;

internal interface IWorldRunEventHandler
{
    WorldRunEventKind Kind { get; }
    void Handle(in WorldRunEvent worldEvent, IWorldRunEventHost host);
}
