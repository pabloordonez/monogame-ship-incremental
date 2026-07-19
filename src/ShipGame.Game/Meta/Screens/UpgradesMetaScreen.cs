using ShipGame.Gameplay;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class UpgradesMetaScreen : MetaScreenHandlerBase
{
    private string? _statusMessage;
    private int _statusFrames;
    private IReadOnlyList<UpgradePreview> _visible = Array.Empty<UpgradePreview>();

    public override MetaScreen Screen => MetaScreen.Upgrades;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        _visible = session.Upgrades.ToArray();

        var columns = _visible.Count > 6 ? 2 : 1;
        var rowsPerColumn = columns == 1
            ? Math.Max(1, _visible.Count)
            : (_visible.Count + columns - 1) / columns;
        var rowHeight = rowsPerColumn > 6 ? 36 : 38;
        var colWidth = columns == 1 ? 592 : 292;
        var gap = 8;

        for (var i = 0; i < _visible.Count; i++)
        {
            var node = _visible[i];
            var col = columns == 1 ? 0 : i / rowsPerColumn;
            var row = columns == 1 ? i : i % rowsPerColumn;
            var x = 24 + col * (colWidth + gap);
            var y = 48 + row * (rowHeight + 4);
            if (y + rowHeight > 312)
                break;

            var ready = !node.Purchased && node.Affordable;
            var upgradeId = node.Definition.Id.Value;
            var cost = node.Definition.Cost;
            var subtitle = node.Purchased
                ? "OWNED"
                : ready
                    ? $"READY  {cost.Ferrite}F {cost.Lumen}L {cost.DataCores}C"
                    : $"NEED  {cost.Ferrite}F {cost.Lumen}L {cost.DataCores}C";
            ui.Add(
                $"upg:{upgradeId}",
                new UiRect(x, y, colWidth, rowHeight),
                $"{MvpPresentation.ShortId(upgradeId)}  {subtitle}",
                ready,
                () =>
                {
                    var result = session.PurchaseUpgrade(context.NextTransactionId("upgrade"), upgradeId);
                    _statusMessage = result.Accepted
                        ? $"Purchased {MvpPresentation.ShortId(upgradeId)}"
                        : result.Message;
                    _statusFrames = 180;
                });
        }

        ui.Add("upgrades:back", new UiRect(24, 320, 160, 28), "Esc  Back", true, () => session.Back());
    }

    public override void Draw(MetaDrawContext context)
    {
        var ui = context.Ui;
        var canvas = context.Canvas;
        canvas.DrawText(24, 16, "UPGRADES", new XnaColor(230, 240, 255), 2);
        canvas.DrawText(24, 36, "Permanent perks from banked resources", new XnaColor(180, 200, 220));
        canvas.DrawBankedPurse(context.Session);

        foreach (var node in _visible)
        {
            var upgradeId = node.Definition.Id.Value;
            var id = $"upg:{upgradeId}";
            var control = canvas.FindControl(ui, id);
            if (control is null)
                continue;

            var ready = !node.Purchased && node.Affordable;
            var cost = node.Definition.Cost;
            var subtitle = node.Purchased
                ? "OWNED"
                : ready
                    ? $"READY  {cost.Ferrite}F {cost.Lumen}L {cost.DataCores}C"
                    : $"NEED  {cost.Ferrite}F {cost.Lumen}L {cost.DataCores}C";
            var accent = node.Purchased
                ? MetaRowAccent.Owned
                : ready
                    ? MetaRowAccent.Ready
                    : MetaRowAccent.Need;
            canvas.TryResolveUiIcon(upgradeId, out var icon);
            canvas.DrawMetaRow(
                control.Bounds,
                ui.GetState(id),
                string.IsNullOrEmpty(icon) ? null : icon,
                MvpPresentation.ShortId(upgradeId),
                subtitle,
                accent);
        }

        canvas.DrawShellButtons(ui);
        if (_statusFrames > 0 && !string.IsNullOrEmpty(_statusMessage))
        {
            canvas.DrawText(200, 326, canvas.Truncate(_statusMessage, 48), new XnaColor(180, 230, 170));
            _statusFrames--;
        }
        else
            canvas.DrawText(200, 326, "Enter/click READY perks", new XnaColor(140, 150, 160));
    }
}
