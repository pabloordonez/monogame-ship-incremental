using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class PauseMetaScreen : MetaScreenHandlerBase
{
    public override MetaScreen Screen => MetaScreen.Pause;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        ui.Add("pause:resume", new UiRect(200, 160, 240, 32), "Esc  Resume", true, () =>
        {
            session.Back();
            context.Run?.SetPaused(false);
        });
        ui.Add("pause:settings", new UiRect(200, 204, 240, 32), "O  Settings", true, () => session.Navigate(MetaScreen.Settings));
    }

    public override void Draw(MetaDrawContext context)
    {
        var canvas = context.Canvas;
        canvas.Fill(0, 0, MvpPresentation.VirtualWidth, MvpPresentation.VirtualHeight, new XnaColor(0, 0, 0, 140));
        canvas.DrawRegion("ui/icons/pause", 304, 48, 32, 32);
        canvas.DrawText(260, 90, "PAUSED", new XnaColor(230, 240, 255), 2);
        canvas.DrawText(200, 120, "Simulation clock stopped.", new XnaColor(180, 200, 220));
        canvas.DrawShellButtons(context.Ui);
    }

    public override void HandleHotkeys(MetaUiContext context, Func<Keys, bool> pressed)
    {
        if (pressed(Keys.O))
        {
            context.Ui.Focus("pause:settings");
            context.Ui.TryActivateFocused();
        }
    }
}
