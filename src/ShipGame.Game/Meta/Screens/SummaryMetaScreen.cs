using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class SummaryMetaScreen : MetaScreenHandlerBase
{
    public override MetaScreen Screen => MetaScreen.Summary;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        context.Ui.Add("summary:station", new UiRect(24, 280, 280, 32), "Enter  Return to Station", true, () =>
        {
            session.Navigate(MetaScreen.Station);
            context.ClearRun();
        });
    }

    public override void Draw(MetaDrawContext context)
    {
        var canvas = context.Canvas;
        var previous = context.Session.Profile.Snapshot.PreviousRun;
        var extracted = previous?.Succeeded == true;
        canvas.DrawScreenBackdrop(extracted ? "backgrounds/summary-extract" : "backgrounds/summary-failed");
        canvas.DrawText(24, 16, "RUN SUMMARY", new XnaColor(230, 240, 255), 2);
        if (previous is null)
        {
            canvas.DrawText(24, 64, "No previous run.", XnaColor.White);
        }
        else
        {
            canvas.DrawText(
                24,
                56,
                extracted ? "RESULT  EXTRACTED" : "RESULT  FAILED",
                extracted ? new XnaColor(140, 220, 160) : new XnaColor(220, 140, 140),
                2);
            canvas.DrawText(24, 88, $"Banked Ferrite {previous.Banked.Ferrite}", XnaColor.White);
            canvas.DrawText(24, 104, $"Banked Lumen   {previous.Banked.Lumen}", XnaColor.White);
            canvas.DrawText(24, 120, $"Banked Cores   {previous.Banked.DataCores}", XnaColor.White);
            canvas.DrawText(24, 144, $"Lost Ferrite   {previous.Lost.Ferrite}", new XnaColor(200, 160, 160));
        }

        canvas.DrawRegion("ui/icons/objective", 520, 56, 48, 48);
        canvas.DrawShellButtons(context.Ui);
    }

    public override void DriveWindowSmoke(MetaUiContext context, int ticks)
    {
        if (ticks > 150)
            context.Session.Navigate(MetaScreen.Station);
    }
}
