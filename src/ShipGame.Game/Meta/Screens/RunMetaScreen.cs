using ShipGame.Domain;
using ShipGame.Gameplay;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace ShipGame.Game;

internal sealed class RunMetaScreen : MetaScreenHandlerBase
{
    public override MetaScreen Screen => MetaScreen.Run;

    public override void BuildUi(MetaUiContext context)
    {
    }

    public override void Draw(MetaDrawContext context)
    {
        var run = context.Run;
        var canvas = context.Canvas;
        var hints = context.Hints;
        if (run is null)
        {
            canvas.DrawPanel("RUN", "Composing encounter...", "Please wait");
            return;
        }

        var bgId = run.EnvironmentId.Value == MetaContentIds.IonVeil
            ? "backgrounds/ion-veil"
            : "backgrounds/cinder-belt";
        var hud = run.Hud;
        var camera = run.Combat.Player != default
            ? run.Combat.Snapshot(run.Combat.Player).Position
            : System.Numerics.Vector2.Zero;

        canvas.DrawParallaxBackground(bgId, camera);
        canvas.UpdateCombatFlash(run, hints);

        foreach (var asteroid in run.Asteroids)
        {
            if (asteroid.Broken)
                continue;
            var screen = canvas.WorldToScreen(new System.Numerics.Vector2(asteroid.X, asteroid.Y), camera);
            if (!canvas.OnScreen(screen, 32))
                continue;
            var region = asteroid.Kind switch
            {
                AsteroidCellKind.Ferrite => "asteroids/medium/ferrite",
                AsteroidCellKind.Lumen => "asteroids/medium/lumen",
                _ => "asteroids/medium/ordinary"
            };
            canvas.DrawRegion(region, (int)screen.X - 12, (int)screen.Y - 12, 24, 24);
        }

        foreach (var pickup in run.Pickups)
        {
            var screen = canvas.WorldToScreen(new System.Numerics.Vector2(pickup.X, pickup.Y), camera);
            if (!canvas.OnScreen(screen, 16))
                continue;
            var region = pickup.ResourceId.Value switch
            {
                MetaContentIds.Lumen => "pickups/lumen",
                MetaContentIds.DataCore => "pickups/data-core",
                _ => "pickups/ferrite"
            };
            canvas.DrawRegion(region, (int)screen.X - 6, (int)screen.Y - 6, 12, 12);
        }

        var playerScreen = new XnaVector2(MvpPresentation.VirtualWidth / 2f, MvpPresentation.VirtualHeight / 2f);
        foreach (var item in run.LiveRenderItems)
        {
            var screen = canvas.WorldToScreen(item.Position, camera);
            if (!canvas.OnScreen(screen, 40))
                continue;
            switch (item.Kind)
            {
                case CombatRenderKind.PlayerShip:
                    playerScreen = screen;
                    canvas.DrawThrustTrail(screen, hints.MoveIntent, hud.RunTick);
                    canvas.DrawRegionRotated("ships/player/wayfarer", screen, item.Rotation, 32);
                    if (hints.FireHeld)
                        canvas.DrawMuzzleFlash(screen, hints.AimDirection, hud.RunTick);
                    if (hints.MineHeld)
                        canvas.DrawMineRay(screen, hints.AimDirection);
                    break;
                case CombatRenderKind.EnemyShip:
                    canvas.DrawRegionRotated(
                        item.Elite ? "enemies/elite-outline" : "enemies/interceptor",
                        screen,
                        item.Rotation,
                        item.Elite ? 28 : 22);
                    break;
                case CombatRenderKind.Projectile:
                    canvas.DrawRegion("projectiles/hostile", (int)screen.X - 3, (int)screen.Y - 3, 6, 6);
                    break;
                case CombatRenderKind.Mine:
                    canvas.DrawRegion("telegraphs/mine-radius", (int)screen.X - 10, (int)screen.Y - 10, 20, 20);
                    break;
            }
        }

        var extract = canvas.WorldToScreen(
            new System.Numerics.Vector2(
                run.Descriptor.Extraction.Center.X * FieldDescriptor.WorldUnitsPerCell,
                run.Descriptor.Extraction.Center.Y * FieldDescriptor.WorldUnitsPerCell),
            camera);
        if (canvas.OnScreen(extract, 40))
            canvas.DrawRegion("field/extraction-marker", (int)extract.X - 16, (int)extract.Y - 16, 32, 32);

        if (hints.ShowAimReticle)
            canvas.DrawAimReticle(hints.MouseVirtual);

        // Flash fade is owned by presentation surface state via UpdateCombatFlash + DrawRunHud path.
        canvas.DrawRunFlashOverlay(hints);

        canvas.DrawRunHud(hud, hints, playerScreen);

        if (hud.Phase == RunPhase.Extraction)
            canvas.DrawText(
                8,
                52,
                $"Hold E in extract zone: {hud.ExtractionProgressTicks}/{hud.ExtractionHoldTicks}",
                new XnaColor(160, 220, 180));

        _ = context.Ui;
        _ = context.Session;
    }

    public override void DriveWindowSmoke(MetaUiContext context, int ticks)
    {
        if (context.Run is null || context.WindowSmokeHarnessStarted || ticks <= 120)
            return;
        context.WindowSmokeHarnessStarted = true;
        var reward = context.Run.CompleteViaHarness(succeed: true);
        context.Session.CommitReward(reward);
        context.WindowSmokeVisitedSummary = true;
        context.ClearRun();
    }
}
