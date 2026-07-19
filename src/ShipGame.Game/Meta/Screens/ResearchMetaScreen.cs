using ShipGame.Gameplay;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class ResearchMetaScreen : MetaScreenHandlerBase
{
    private string? _statusMessage;
    private int _statusFrames;
    private IReadOnlyList<ResearchPreview> _visible = Array.Empty<ResearchPreview>();

    public override MetaScreen Screen => MetaScreen.Research;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        _visible = session.Research
            .Where(node => node.Purchased || (node.PrerequisitesMet && node.GateMet))
            .ToArray();

        var columns = _visible.Count > 6 ? 2 : 1;
        var rowsPerColumn = columns == 1
            ? Math.Max(1, _visible.Count)
            : (_visible.Count + 1) / 2;
        var rowHeight = rowsPerColumn > 6 ? 36 : 40;
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

            var ready = !node.Purchased && node.Affordable && node.PrerequisitesMet && node.GateMet;
            var researchId = node.Definition.Id;
            var cost = node.Definition.Cost;
            var subtitle = node.Purchased
                ? "OWNED"
                : ready
                    ? $"READY  {cost.Ferrite}F {cost.Lumen}L {cost.DataCores}C"
                    : $"NEED  {cost.Ferrite}F {cost.Lumen}L {cost.DataCores}C";
            ui.Add(
                $"research:{researchId}",
                new UiRect(x, y, colWidth, rowHeight),
                $"{MvpPresentation.ShortId(researchId)}  {subtitle}",
                ready,
                () =>
                {
                    var result = session.PurchaseResearch(context.NextTransactionId("research"), researchId);
                    _statusMessage = result.Accepted
                        ? $"Unlocked {MvpPresentation.ShortId(researchId)}"
                        : result.Message;
                    _statusFrames = 180;
                });
        }

        ui.Add("research:back", new UiRect(24, 320, 160, 28), "Esc  Back", true, () => session.Back());
    }

    public override void Draw(MetaDrawContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        var canvas = context.Canvas;
        canvas.DrawScreenBackdrop("backgrounds/research-lab", dimAlpha: 200);
        canvas.DrawText(24, 16, "RESEARCH", new XnaColor(230, 240, 255), 2);
        canvas.DrawText(24, 36, "Unlock modules for your loadout", new XnaColor(180, 200, 220));
        canvas.DrawBankedPurse(session);

        string? tooltipTitle = null;
        string? tooltipBody = null;
        string? tooltipStatus = null;
        foreach (var node in _visible)
        {
            var id = $"research:{node.Definition.Id}";
            var control = canvas.FindControl(ui, id);
            if (control is null)
                continue;

            var ready = !node.Purchased && node.Affordable && node.PrerequisitesMet && node.GateMet;
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
            canvas.TryResolveUiIcon(node.Definition.Id, out var icon);
            canvas.DrawMetaRow(
                control.Bounds,
                ui.GetState(id),
                string.IsNullOrEmpty(icon) ? null : icon,
                MvpPresentation.ShortId(node.Definition.Id),
                subtitle,
                accent);

            if (ui.GetState(id) is UiControlState.Focused or UiControlState.Hovered)
            {
                tooltipTitle = node.Definition.Name;
                tooltipBody = MetaItemDescriptions.For(node.Definition.Id);
                tooltipStatus = node.Purchased ? "OWNED" : ready ? "READY" : "LOCKED";
            }
        }

        if (tooltipTitle is not null && tooltipBody is not null)
            canvas.DrawItemTooltip(tooltipTitle, tooltipBody, tooltipStatus);

        canvas.DrawShellButtons(ui);
        if (_statusFrames > 0 && !string.IsNullOrEmpty(_statusMessage))
        {
            canvas.DrawText(200, 326, canvas.Truncate(_statusMessage, 48), new XnaColor(180, 230, 170));
            _statusFrames--;
        }
        else
            canvas.DrawText(200, 326, "Enter/click READY nodes", new XnaColor(140, 150, 160));
    }
}
