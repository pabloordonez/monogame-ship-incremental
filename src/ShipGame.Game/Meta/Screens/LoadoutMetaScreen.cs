using ShipGame.Domain;
using ShipGame.Gameplay;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class LoadoutMetaScreen : MetaScreenHandlerBase
{
    private static readonly ModuleSlot[] Slots =
    [
        ModuleSlot.Weapon, ModuleSlot.Mining, ModuleSlot.Shield, ModuleSlot.Engine, ModuleSlot.Utility
    ];

    private readonly List<(ModuleSlot Slot, LoadoutPreview Preview, bool Equipped)> _rows = [];
    private string? _statusMessage;
    private int _statusFrames;

    public override MetaScreen Screen => MetaScreen.Loadout;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        _rows.Clear();

        // Two columns so five slots with alternates fit above the back button.
        var leftSlots = Slots.Take(3).ToArray();
        var rightSlots = Slots.Skip(3).ToArray();
        LayoutColumn(context, leftSlots, x: 24, startY: 52, width: 310);
        LayoutColumn(context, rightSlots, x: 346, startY: 52, width: 166);

        ui.Add("loadout:back", new UiRect(24, 320, 160, 28), "Esc  Back", true, () => session.Back());
    }

    private void LayoutColumn(
        MetaUiContext context,
        ModuleSlot[] slots,
        int x,
        int startY,
        int width)
    {
        var session = context.Session;
        var ui = context.Ui;
        var y = startY;
        foreach (var slot in slots)
        {
            if (y > 300)
                break;

            y += 14; // header band
            foreach (var preview in session.Ui.BuildLoadoutView(slot))
            {
                if (!preview.Unlocked || !preview.Compatible || !preview.Known)
                    continue;
                if (y > 300)
                    break;

                var equipped = string.Equals(
                    context.EffectiveModule(slot),
                    preview.ModuleId,
                    StringComparison.Ordinal);
                var moduleId = preview.ModuleId;
                var moduleSlot = slot;
                _rows.Add((slot, preview, equipped));
                ui.Add(
                    $"loadout:{slot}:{moduleId}",
                    new UiRect(x, y, width, 30),
                    $"{(equipped ? "EQUIPPED" : "Equip")} {slot} {MvpPresentation.ShortId(moduleId)}",
                    true,
                    () =>
                    {
                        if (equipped)
                            return;
                        var result = session.EquipModule(
                            context.NextTransactionId("equip"),
                            moduleSlot,
                            moduleId);
                        _statusMessage = result.Accepted
                            ? $"Equipped {MvpPresentation.ShortId(moduleId)}"
                            : result.Message;
                        _statusFrames = 180;
                    });
                y += 32;
            }

            y += 6;
        }
    }

    public override void Draw(MetaDrawContext context)
    {
        var ui = context.Ui;
        var canvas = context.Canvas;
        canvas.DrawText(24, 16, "LOADOUT", new XnaColor(230, 240, 255), 2);
        canvas.DrawText(24, 36, "Equip unlocked modules", new XnaColor(180, 200, 220));
        canvas.DrawBankedPurse(context.Session);
        canvas.DrawRegion("ships/player/wayfarer", 520, 48, 72, 72);

        ModuleSlot? lastSlot = null;
        string? focusedHint = null;
        foreach (var (slot, preview, equipped) in _rows)
        {
            var id = $"loadout:{slot}:{preview.ModuleId}";
            var control = canvas.FindControl(ui, id);
            if (control is null)
                continue;

            if (lastSlot != slot)
            {
                lastSlot = slot;
                var headerY = control.Bounds.Y - 12;
                var slotId = MvpPresentation.SlotCatalogId(slot);
                canvas.TryResolveUiIcon(slotId, out var slotIcon);
                if (!string.IsNullOrEmpty(slotIcon))
                    canvas.DrawRegion(slotIcon, control.Bounds.X, headerY - 1, 12, 12);
                canvas.DrawText(
                    control.Bounds.X + (string.IsNullOrEmpty(slotIcon) ? 0 : 16),
                    headerY,
                    slot.ToString().ToUpperInvariant(),
                    new XnaColor(160, 180, 200));
            }

            canvas.TryResolveUiIcon(preview.ModuleId, out var icon);
            if (string.IsNullOrEmpty(icon))
                canvas.TryResolveUiIcon(MvpPresentation.SlotCatalogId(slot), out icon);

            canvas.DrawMetaRow(
                control.Bounds,
                ui.GetState(id),
                string.IsNullOrEmpty(icon) ? null : icon,
                MvpPresentation.ShortId(preview.ModuleId),
                equipped ? "EQUIPPED" : "Available",
                equipped ? MetaRowAccent.Equipped : MetaRowAccent.Ready);

            if (ui.GetState(id) is UiControlState.Focused or UiControlState.Hovered)
            {
                focusedHint = equipped
                    ? "Currently equipped"
                    : FormatStatHint(preview)
                      ?? (string.IsNullOrWhiteSpace(preview.Explanation)
                          ? "Enter/click to equip"
                          : preview.Explanation);
            }
        }

        canvas.DrawShellButtons(ui);
        if (_statusFrames > 0 && !string.IsNullOrEmpty(_statusMessage))
        {
            canvas.DrawText(200, 326, canvas.Truncate(_statusMessage, 52), new XnaColor(180, 230, 170));
            _statusFrames--;
        }
        else
        {
            var footer = focusedHint ?? "Enter/click to equip";
            canvas.DrawText(200, 326, canvas.Truncate(footer, 52), new XnaColor(140, 150, 160));
        }
    }

    private static string? FormatStatHint(LoadoutPreview preview)
    {
        if (preview.Proposed is null)
            return null;
        var current = preview.Current;
        var proposed = preview.Proposed;
        return $"Hull {current.MaximumHull}->{proposed.MaximumHull}  Shield {current.ShieldCapacity}->{proposed.ShieldCapacity}";
    }
}
