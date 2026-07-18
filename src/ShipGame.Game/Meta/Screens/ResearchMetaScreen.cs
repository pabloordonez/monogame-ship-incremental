using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class ResearchMetaScreen : MetaScreenHandlerBase
{
    public override MetaScreen Screen => MetaScreen.Research;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        var y = 48;
        foreach (var node in session.Research.Take(10))
        {
            var ready = !node.Purchased && node.Affordable && node.PrerequisitesMet && node.GateMet;
            var cost = $"{node.Definition.Cost.Ferrite}F/{node.Definition.Cost.Lumen}L/{node.Definition.Cost.DataCores}C";
            var status = node.Purchased ? "OWNED" : ready ? $"READY {cost}" : node.Affordable ? "LOCKED" : $"NEED {cost}";
            var researchId = node.Definition.Id;
            ui.Add(
                $"research:{researchId}",
                new UiRect(24, y, 592, 22),
                $"{MvpPresentation.ShortId(researchId)}  {status}",
                ready,
                () => session.PurchaseResearch(context.NextTransactionId("research"), researchId));
            y += 24;
        }

        ui.Add("research:back", new UiRect(24, 320, 160, 28), "Esc  Back", true, () => session.Back());
    }

    public override void Draw(MetaDrawContext context)
    {
        var canvas = context.Canvas;
        canvas.DrawText(24, 16, "RESEARCH", new XnaColor(230, 240, 255), 2);
        canvas.DrawText(24, 36, "Spend banked Ferrite/Lumen/Cores — unlocks loadout modules", new XnaColor(180, 200, 220));
        canvas.DrawBankedPurse(context.Session);
        canvas.DrawShellButtons(context.Ui);
        canvas.DrawText(24, 340, "Enter/click purchase when READY  Esc station", new XnaColor(140, 150, 160));
    }
}
