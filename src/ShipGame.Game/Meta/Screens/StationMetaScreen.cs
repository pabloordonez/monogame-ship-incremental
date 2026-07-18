using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class StationMetaScreen : MetaScreenHandlerBase
{
    public override MetaScreen Screen => MetaScreen.Station;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        ui.Add("station:map", new UiRect(24, 170, 280, 32), "M  Environment Map", true, () => session.Navigate(MetaScreen.Map));
        ui.Add("station:loadout", new UiRect(24, 210, 280, 32), "L  Loadout", true, () => session.Navigate(MetaScreen.Loadout));
        ui.Add("station:research", new UiRect(24, 250, 280, 32), "R  Research", true, () => session.Navigate(MetaScreen.Research));
        ui.Add("station:upgrades", new UiRect(24, 290, 280, 32), "U  Upgrades", true, () => session.Navigate(MetaScreen.Upgrades));
        ui.Add("station:settings", new UiRect(320, 170, 280, 32), "O  Settings", true, () => session.Navigate(MetaScreen.Settings));
    }

    public override void Draw(MetaDrawContext context)
    {
        var session = context.Session;
        var canvas = context.Canvas;
        var station = session.Station;
        canvas.DrawText(24, 16, "STATION", new XnaColor(230, 240, 255), 2);
        canvas.DrawText(24, 36, "Banked materials from the field", new XnaColor(160, 180, 200));
        canvas.DrawRegion("ships/player/wayfarer", 520, 40, 72, 72);
        canvas.DrawRegion("ui/icons/resource-ferrite", 24, 56, 16, 16);
        canvas.DrawText(46, 58, $"Ferrite {station.Balances.Ferrite}", new XnaColor(220, 200, 160));
        canvas.DrawRegion("ui/icons/resource-lumen", 24, 78, 16, 16);
        canvas.DrawText(46, 80, $"Lumen {station.Balances.Lumen}", new XnaColor(180, 220, 255));
        canvas.DrawRegion("ui/icons/resource-data-core", 24, 100, 16, 16);
        canvas.DrawText(46, 102, $"Cores {station.Balances.DataCores}", new XnaColor(200, 180, 255));
        if (station.PreviousRun is { } previous)
            canvas.DrawText(
                24,
                128,
                previous.Succeeded ? "Last run: EXTRACTED" : "Last run: FAILED",
                previous.Succeeded ? new XnaColor(140, 220, 160) : new XnaColor(220, 140, 140));

        canvas.DrawText(24, 148, "Spend banked resources between flights", new XnaColor(180, 200, 220));
        canvas.DrawShellButtons(context.Ui);
        canvas.DrawText(24, 340, "Arrows focus  Enter/click activate", new XnaColor(140, 150, 160));
    }

    public override void HandleHotkeys(MetaUiContext context, Func<Keys, bool> pressed)
    {
        var ui = context.Ui;
        if (pressed(Keys.M))
        {
            ui.Focus("station:map");
            ui.TryActivateFocused();
        }
        else if (pressed(Keys.L))
        {
            ui.Focus("station:loadout");
            ui.TryActivateFocused();
        }
        else if (pressed(Keys.R))
        {
            ui.Focus("station:research");
            ui.TryActivateFocused();
        }
        else if (pressed(Keys.U))
        {
            ui.Focus("station:upgrades");
            ui.TryActivateFocused();
        }
        else if (pressed(Keys.O))
        {
            ui.Focus("station:settings");
            ui.TryActivateFocused();
        }
    }

    public override void DriveWindowSmoke(MetaUiContext context, int ticks)
    {
        if (ticks > 60)
            context.Session.Navigate(MetaScreen.Map);
    }
}
