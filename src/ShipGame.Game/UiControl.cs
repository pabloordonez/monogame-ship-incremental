namespace ShipGame.Game;

public sealed class UiControl
{
    public UiControl(string id, UiRect bounds, string label, bool enabled, Action activate)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Bounds = bounds;
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Enabled = enabled;
        Activate = activate ?? throw new ArgumentNullException(nameof(activate));
    }

    public string Id { get; }
    public UiRect Bounds { get; }
    public string Label { get; }
    public bool Enabled { get; }
    public Action Activate { get; }
}
