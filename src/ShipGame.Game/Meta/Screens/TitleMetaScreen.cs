using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace ShipGame.Game;

internal sealed class TitleMetaScreen : MetaScreenHandlerBase
{
    private const float EnterDuration = 0.9f;
    private const float SettleDuration = 0.3f;
    private const float RevealDuration = 0.6f;
    private const float InteractiveAt = EnterDuration + SettleDuration + RevealDuration;
    private const float ShipRestAlpha = 0.45f;
    private const int WordmarkWidth = 384;
    private const int WordmarkHeight = 96;
    private const int WordmarkRestY = 28;
    private const int ShipSize = 148;
    private const int ShipRestCenterY = 250;
    private const int ButtonX = 200;
    private const int ButtonWidth = 240;
    private const int ButtonHeight = 32;
    private const int ButtonStartY = 168;

    private float _elapsed;
    private bool _interactive;
    private bool _deferInteractive;

    public override MetaScreen Screen => MetaScreen.Title;

    public override void Update(MetaUiContext context)
    {
        if (_deferInteractive)
        {
            _deferInteractive = false;
            _interactive = true;
            return;
        }

        if (context.WindowSmoke || context.WindowSmokeHarnessStarted)
        {
            FinishSplash();
            return;
        }

        if (_interactive)
            return;

        if (context.ActivatePressed || context.PointerPressed)
        {
            // Defer one frame so the skip press does not also activate a brand-new button.
            FinishSplash(deferInteractive: true);
            return;
        }

        _elapsed += Math.Max(0f, context.DeltaSeconds);
        if (_elapsed >= InteractiveAt)
            FinishSplash();
    }

    public override void BuildUi(MetaUiContext context)
    {
        if (!_interactive)
            return;

        var session = context.Session;
        var ui = context.Ui;
        var y = ButtonStartY;

        if (session.HasContinueSave)
        {
            ui.Add(
                "title:continue",
                new UiRect(ButtonX, y, ButtonWidth, ButtonHeight),
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
            new UiRect(ButtonX, y, ButtonWidth, ButtonHeight),
            session.HasContinueSave ? "N  New Game" : "Enter  New Game",
            true,
            () =>
            {
                var created = session.CreateNewProfile();
                if (created.Accepted)
                    session.Navigate(MetaScreen.Station);
            });
        y += 40;

        ui.Add("title:quit", new UiRect(ButtonX, y, ButtonWidth, ButtonHeight), "Esc  Quit", true, context.ExitGame);
    }

    public override void Draw(MetaDrawContext context)
    {
        var session = context.Session;
        var canvas = context.Canvas;
        var enterT = EaseOutBounce(Math.Clamp(_elapsed / EnterDuration, 0f, 1f));
        var logoY = (int)MathF.Round(Lerp(-WordmarkHeight - 8f, WordmarkRestY, enterT));
        var shipCenterY = Lerp(MvpPresentation.VirtualHeight + ShipSize * 0.5f, ShipRestCenterY, enterT);
        var shipCenter = new XnaVector2(MvpPresentation.VirtualWidth * 0.5f, shipCenterY);

        var revealT = 0f;
        if (_elapsed > EnterDuration + SettleDuration)
        {
            revealT = Math.Clamp(
                (_elapsed - EnterDuration - SettleDuration) / RevealDuration,
                0f,
                1f);
        }

        if (_interactive)
            revealT = 1f;

        // Arcade depth: dim starfield behind the splash cast.
        var drift = _elapsed * 12f;
        canvas.DrawParallaxBackground("backgrounds/ion-veil", new System.Numerics.Vector2(drift, drift * 0.35f));
        canvas.Fill(0, 0, MvpPresentation.VirtualWidth, MvpPresentation.VirtualHeight, new XnaColor(8, 4, 20, 140));

        var shipAlpha = Lerp(1f, ShipRestAlpha, revealT);
        var shipTint = XnaColor.White * shipAlpha;
        canvas.DrawRegionRotated("ships/player/engine", shipCenter, -0.2f, ShipSize, shipTint);
        canvas.DrawRegionRotated("ships/player/wayfarer", shipCenter, -0.2f, ShipSize, shipTint);

        var logoX = (MvpPresentation.VirtualWidth - WordmarkWidth) / 2;
        canvas.DrawTexture("ui/title/wordmark", logoX, logoY, WordmarkWidth, WordmarkHeight, XnaColor.White);

        if (revealT > 0f)
        {
            var tagline = session.HasContinueSave
                ? "Continue your save or start fresh"
                : "Start a new expedition";
            var tagAlpha = (byte)Math.Clamp((int)(revealT * 230f), 0, 255);
            canvas.DrawText(168, 132, tagline, new XnaColor((byte)255, (byte)196, (byte)72, tagAlpha));
        }

        if (_interactive)
            canvas.DrawShellButtons(context.Ui);

        DrawScanlines(canvas);
    }

    private static void DrawScanlines(IMetaScreenCanvas canvas)
    {
        var line = new XnaColor((byte)0, (byte)0, (byte)0, (byte)38);
        for (var y = 0; y < MvpPresentation.VirtualHeight; y += 2)
            canvas.Fill(0, y, MvpPresentation.VirtualWidth, 1, line);
    }

    public override void HandleHotkeys(MetaUiContext context, Func<Keys, bool> pressed)
    {
        if (!_interactive)
            return;

        if (pressed(Keys.N))
        {
            context.Ui.Focus("title:new");
            context.Ui.TryActivateFocused();
        }
    }

    public override void DriveWindowSmoke(MetaUiContext context, int ticks)
    {
        FinishSplash();
        if (ticks <= 30)
            return;
        var session = context.Session;
        if (session.HasContinueSave)
            session.Navigate(MetaScreen.Station);
        else if (session.CreateNewProfile().Accepted)
            session.Navigate(MetaScreen.Station);
    }

    private void FinishSplash(bool deferInteractive = false)
    {
        _elapsed = InteractiveAt;
        if (deferInteractive)
            _deferInteractive = true;
        else
            _interactive = true;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Standard ease-out bounce for splash entrances.</summary>
    private static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1f / d1)
            return n1 * t * t;
        if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return n1 * t * t + 0.75f;
        }

        if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return n1 * t * t + 0.9375f;
        }

        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }
}
