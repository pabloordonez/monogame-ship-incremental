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
        ui.Add("title:new", new UiRect(200, 180, 240, 32), "Enter  New Game / Station", true, () =>
        {
            if (session.RequiresExplicitNewProfile)
                session.CreateNewProfile();
            session.Navigate(MetaScreen.Station);
        });
        ui.Add(
            "title:continue",
            new UiRect(200, 220, 240, 32),
            "C  Continue Save",
            !session.RequiresExplicitNewProfile,
            () =>
            {
                session.ContinueFromDisk();
                if (session.Screen == MetaScreen.Title && !session.RequiresExplicitNewProfile)
                    session.Navigate(MetaScreen.Station);
            });
        ui.Add("title:quit", new UiRect(200, 260, 240, 32), "Esc  Quit", true, context.ExitGame);
    }

    public override void Draw(MetaDrawContext context)
    {
        var canvas = context.Canvas;
        canvas.DrawRegion("ui/icons/objective", 296, 48, 48, 48);
        canvas.DrawText(220, 110, "SHIP GAME", new XnaColor(240, 245, 255), 2);
        canvas.DrawText(200, 150, "Mouse or keyboard to choose", new XnaColor(160, 180, 200));
        canvas.DrawShellButtons(context.Ui);
        canvas.DrawRegion("ui/icons/interact", 304, 300, 32, 32);
    }

    public override void HandleHotkeys(MetaUiContext context, Func<Keys, bool> pressed)
    {
        if (pressed(Keys.C))
        {
            context.Ui.Focus("title:continue");
            context.Ui.TryActivateFocused();
        }
    }

    public override void DriveWindowSmoke(MetaUiContext context, int ticks)
    {
        if (ticks > 30)
            context.Session.Navigate(MetaScreen.Station);
    }
}
