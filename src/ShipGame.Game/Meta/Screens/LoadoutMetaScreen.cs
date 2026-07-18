using ShipGame.Domain;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class LoadoutMetaScreen : MetaScreenHandlerBase
{
    public override MetaScreen Screen => MetaScreen.Loadout;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        var y = 48;
        foreach (var slot in new[]
                 {
                     ModuleSlot.Weapon, ModuleSlot.Mining, ModuleSlot.Shield, ModuleSlot.Engine, ModuleSlot.Utility
                 })
        {
            foreach (var preview in session.Ui.BuildLoadoutView(slot))
            {
                if (y > 300)
                    break;
                var equipped = string.Equals(
                    context.EffectiveModule(slot),
                    preview.ModuleId,
                    StringComparison.Ordinal);
                var ready = preview.Unlocked && preview.Compatible && preview.Known;
                var label =
                    $"{(equipped ? "*" : " ")} {slot} {MvpPresentation.ShortId(preview.ModuleId)}" +
                    (ready ? "" : " [locked]");
                var moduleId = preview.ModuleId;
                var moduleSlot = slot;
                ui.Add(
                    $"loadout:{slot}:{moduleId}",
                    new UiRect(24, y, 480, 22),
                    label,
                    ready && !equipped,
                    () => session.EquipModule(context.NextTransactionId("equip"), moduleSlot, moduleId));
                y += 24;
            }
        }

        ui.Add("loadout:back", new UiRect(24, 320, 160, 28), "Esc  Back", true, () => session.Back());
    }

    public override void Draw(MetaDrawContext context)
    {
        var canvas = context.Canvas;
        canvas.DrawText(24, 16, "LOADOUT", new XnaColor(230, 240, 255), 2);
        canvas.DrawText(24, 36, "Equip modules unlocked via Research (no extra cost)", new XnaColor(180, 200, 220));
        canvas.DrawBankedPurse(context.Session);
        canvas.DrawRegion("ships/player/wayfarer", 520, 48, 72, 72);
        canvas.DrawShellButtons(context.Ui);
        canvas.DrawText(24, 340, "Enter/click equip unlocked module  Esc station", new XnaColor(140, 150, 160));
    }
}
