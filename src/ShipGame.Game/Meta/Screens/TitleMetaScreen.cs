using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class TitleMetaScreen : MetaScreenHandlerBase
{
    public override MetaScreen Screen => MetaScreen.Title;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        var y = 180;

        if (session.HasContinueSave)
        {
            ui.Add(
                "title:continue",
                new UiRect(200, y, 240, 32),
                "Enter  Continue",
                true,
                () =>
                {
                    session.ContinueFromDisk();
                    if (session.HasContinueSave)
                        session.Navigate(MetaScreen.Station);
                });
            y += 40;
        }

        ui.Add(
            "title:new",
            new UiRect(200, y, 240, 32),
            session.HasContinueSave ? "N  New Game" : "Enter  New Game",
            true,
            () =>
            {
                var created = session.CreateNewProfile();
                if (created.Accepted)
                    session.Navigate(MetaScreen.Station);
            });
        y += 40;

        ui.Add("title:quit", new UiRect(200, y, 240, 32), "Esc  Quit", true, context.ExitGame);
    }

    public override void Draw(MetaDrawContext context)
    {
        var session = context.Session;
        var canvas = context.Canvas;
        canvas.DrawRegion("ui/icons/objective", 296, 48, 48, 48);
        canvas.DrawText(220, 110, "SHIP GAME", new XnaColor(240, 245, 255), 2);
        canvas.DrawText(
            168,
            150,
            session.HasContinueSave ? "Continue your save or start fresh" : "Start a new expedition",
            new XnaColor(160, 180, 200));
        canvas.DrawShellButtons(context.Ui);
        canvas.DrawRegion("ui/icons/interact", 304, 300, 32, 32);
    }

    public override void HandleHotkeys(MetaUiContext context, Func<Keys, bool> pressed)
    {
        if (pressed(Keys.N))
        {
            context.Ui.Focus("title:new");
            context.Ui.TryActivateFocused();
        }
    }

    public override void DriveWindowSmoke(MetaUiContext context, int ticks)
    {
        if (ticks <= 30)
            return;
        var session = context.Session;
        if (session.HasContinueSave)
            session.Navigate(MetaScreen.Station);
        else if (session.CreateNewProfile().Accepted)
            session.Navigate(MetaScreen.Station);
    }
}
