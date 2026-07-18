using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class UpgradesMetaScreen : MetaScreenHandlerBase
{
    public override MetaScreen Screen => MetaScreen.Upgrades;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        var y = 48;
        foreach (var node in session.Upgrades.Take(10))
        {
            var ready = !node.Purchased && node.Affordable;
            var cost = $"{node.Definition.Cost.Ferrite}F/{node.Definition.Cost.Lumen}L/{node.Definition.Cost.DataCores}C";
            var status = node.Purchased ? "OWNED" : ready ? $"READY {cost}" : $"NEED {cost}";
            var upgradeId = node.Definition.Id.Value;
            ui.Add(
                $"upg:{upgradeId}",
                new UiRect(24, y, 592, 22),
                $"{MvpPresentation.ShortId(upgradeId)}  {status}",
                ready,
                () => session.PurchaseUpgrade(context.NextTransactionId("upgrade"), upgradeId));
            y += 24;
        }

        ui.Add("upgrades:back", new UiRect(24, 320, 160, 28), "Esc  Back", true, () => session.Back());
    }

    public override void Draw(MetaDrawContext context)
    {
        var canvas = context.Canvas;
        canvas.DrawText(24, 16, "UPGRADES", new XnaColor(230, 240, 255), 2);
        canvas.DrawText(24, 36, "Permanent perks — paid with banked Ferrite/Lumen/Cores", new XnaColor(180, 200, 220));
        canvas.DrawBankedPurse(context.Session);
        canvas.DrawShellButtons(context.Ui);
        canvas.DrawText(24, 340, "Enter/click purchase when READY  Esc station", new XnaColor(140, 150, 160));
    }
}
