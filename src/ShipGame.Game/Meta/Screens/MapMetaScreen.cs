using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class MapMetaScreen : MetaScreenHandlerBase
{
    public override MetaScreen Screen => MetaScreen.Map;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        var y = 56;
        foreach (var env in session.Map)
        {
            var envId = env.EnvironmentId;
            var label = MvpPresentation.ShortId(envId);
            var prefix = env.Selected ? "> " : "  ";
            ui.Add(
                $"env:{envId}",
                new UiRect(24, y, 592, 48),
                $"{prefix}{label}",
                true,
                () =>
                {
                    if (env.Accessible)
                        session.SelectEnvironment(envId);
                });
            y += 56;
        }

        ui.Add("map:launch", new UiRect(24, 280, 220, 32), "Enter  Launch", true, () =>
        {
            if (session.Launch().Accepted)
                context.StartRun();
        });
        ui.Add("map:back", new UiRect(260, 280, 160, 32), "Esc  Back", true, () => session.Back());
    }

    public override void Draw(MetaDrawContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        var canvas = context.Canvas;
        canvas.DrawText(24, 16, "SELECT ENVIRONMENT", new XnaColor(230, 240, 255), 2);
        foreach (var env in session.Map)
        {
            var id = $"env:{env.EnvironmentId}";
            var control = canvas.FindControl(ui, id);
            if (control is null)
                continue;
            var state = ui.GetState(id);
            canvas.DrawButton(control.Bounds, control.Label, state);
            if (!env.Accessible)
            {
                canvas.DrawRegion("ui/icons/lock", control.Bounds.X + control.Bounds.Width - 36, control.Bounds.Y + 10, 20, 20);
                canvas.DrawText(
                    control.Bounds.X + 12,
                    control.Bounds.Y + 28,
                    canvas.Truncate(env.Explanation, 64),
                    new XnaColor(160, 160, 170));
            }
            else
                canvas.DrawText(
                    control.Bounds.X + 12,
                    control.Bounds.Y + 28,
                    env.Selected ? "Selected — Launch when ready" : "Select then Launch",
                    new XnaColor(180, 190, 200));
        }

        canvas.DrawShellButtons(ui, skipPrefix: "env:");
        canvas.DrawText(24, 340, "Up/Down select  Enter/click Launch  Esc station", new XnaColor(140, 150, 160));
    }

    public override void DriveWindowSmoke(MetaUiContext context, int ticks)
    {
        if (ticks > 90 && context.Session.Launch().Accepted)
            context.StartRun();
    }
}
