using ShipGame.Domain;
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
            ui.Add(
                $"env:{envId}",
                new UiRect(24, y, 592, 48),
                EnvironmentTitle(envId),
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

            var accent = !env.Accessible
                ? MetaRowAccent.Need
                : env.Selected
                    ? MetaRowAccent.Owned
                    : MetaRowAccent.Ready;
            var subtitle = !env.Accessible
                ? env.Explanation
                : env.Selected
                    ? "SELECTED"
                    : "AVAILABLE";
            canvas.DrawMetaRow(
                control.Bounds,
                ui.GetState(id),
                null,
                EnvironmentTitle(env.EnvironmentId),
                subtitle,
                accent);
            if (!env.Accessible)
                canvas.DrawRegion(
                    "ui/icons/lock",
                    control.Bounds.X + control.Bounds.Width - 36,
                    control.Bounds.Y + 10,
                    20,
                    20);
        }

        canvas.DrawShellButtons(ui, skipPrefix: "env:");
        canvas.DrawText(24, 340, "Click select  Enter Launch  Esc station", new XnaColor(140, 150, 160));
    }

    public override void DriveWindowSmoke(MetaUiContext context, int ticks)
    {
        if (ticks > 90 && context.Session.Launch().Accepted)
            context.StartRun();
    }

    private static string EnvironmentTitle(string environmentId) => environmentId switch
    {
        MetaContentIds.CinderBelt => "Cinder Belt",
        MetaContentIds.IonVeil => "Ion Veil",
        _ => MvpPresentation.ShortId(environmentId)
    };
}
