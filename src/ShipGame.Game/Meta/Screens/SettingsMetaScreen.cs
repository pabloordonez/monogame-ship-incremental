using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal sealed class SettingsMetaScreen : MetaScreenHandlerBase
{
    public override MetaScreen Screen => MetaScreen.Settings;

    public override void BuildUi(MetaUiContext context)
    {
        var session = context.Session;
        var ui = context.Ui;
        var settings = session.Profile.Snapshot.Settings;
        ui.Add(
            "settings:shake",
            new UiRect(24, 70, 400, 28),
            $"Screen Shake  {(settings.ScreenShake ? "ON" : "OFF")}",
            true,
            () => context.ApplySettings(settings with { ScreenShake = !settings.ScreenShake }));
        ui.Add(
            "settings:flashes",
            new UiRect(24, 106, 400, 28),
            $"Flashes  {(settings.Flashes ? "ON" : "OFF")}",
            true,
            () => context.ApplySettings(settings with { Flashes = !settings.Flashes }));
        ui.Add(
            "settings:vibration",
            new UiRect(24, 142, 400, 28),
            $"Vibration  {(settings.Vibration ? "ON" : "OFF")}",
            true,
            () => context.ApplySettings(settings with { Vibration = !settings.Vibration }));
        ui.Add(
            "settings:telemetry",
            new UiRect(24, 178, 400, 28),
            $"Telemetry Consent  {(settings.TelemetryConsent ? "ON" : "OFF")}",
            true,
            () => context.ApplySettings(settings with { TelemetryConsent = !settings.TelemetryConsent }));
        ui.Add(
            "settings:master",
            new UiRect(24, 214, 400, 28),
            $"Master Volume  {settings.MasterVolume}",
            true,
            () =>
            {
                var next = settings.MasterVolume <= 0 ? 100 : Math.Max(0, settings.MasterVolume - 20);
                context.ApplySettings(settings with { MasterVolume = next });
            });
        ui.Add("settings:back", new UiRect(24, 280, 160, 28), "Esc  Back", true, () => session.Back());
    }

    public override void Draw(MetaDrawContext context)
    {
        var canvas = context.Canvas;
        canvas.DrawText(24, 16, "SETTINGS", new XnaColor(230, 240, 255), 2);
        canvas.DrawText(24, 44, "Toggle accessibility and audio options.", new XnaColor(180, 200, 220));
        canvas.DrawShellButtons(context.Ui);
        canvas.DrawText(24, 340, "Enter/click toggle  Esc back", new XnaColor(140, 150, 160));
        _ = context.Session;
    }
}
