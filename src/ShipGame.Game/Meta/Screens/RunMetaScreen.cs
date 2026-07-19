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
            if (!canvas.OnScreen(screen, 20))
                continue;
            var region = pickup.ResourceId.Value switch
            {
                MetaContentIds.Lumen => "pickups/lumen",
                MetaContentIds.DataCore => "pickups/data-core",
                _ => "pickups/ferrite"
            };
            canvas.DrawRegion(region, (int)screen.X - 8, (int)screen.Y - 8, 16, 16);
        }

        var playerScreen = new XnaVector2(MvpPresentation.VirtualWidth / 2f, MvpPresentation.VirtualHeight / 2f);
        var anyHostileOnScreen = false;
        foreach (var item in run.LiveRenderItems)
        {
            var screen = canvas.WorldToScreen(item.Position, camera);
            var onScreen = canvas.OnScreen(screen, 40);
            if (item.Kind == CombatRenderKind.EnemyShip && !item.Elite && onScreen)
                anyHostileOnScreen = true;
            if (!onScreen)
                continue;
            switch (item.Kind)
            {
                case CombatRenderKind.PlayerShip:
                    playerScreen = screen;
                    canvas.DrawThrustTrail(screen, hints.MoveIntent, hud.RunTick);
                    canvas.DrawRegionRotated("ships/player/wayfarer", screen, item.Rotation, 32);
                    if (run.HasTractorUtility)
                    {
                        var tractorOffset = new XnaVector2(MathF.Cos(item.Rotation), MathF.Sin(item.Rotation)) * 14f;
                        canvas.DrawRegionRotated(
                            "effects/tractor",
                            screen + tractorOffset,
                            item.Rotation + MathF.Sin(hud.RunTick * 0.12f) * 0.15f,
                            20);
                    }
                    if (hints.FireHeld)
                    {
                        if (run.Combat.TryGetPlayerWeapon(out var weapon, out var beamRange) &&
                            weapon == WeaponBehavior.Beam)
                        {
                            float? hit = run.Combat.TryGetPlayerBeamHitDistance(out var hitDistance)
                                ? hitDistance
                                : null;
                            canvas.DrawBeamRay(screen, hints.AimDirection, beamRange, hit);
                        }
                        else
                            canvas.DrawMuzzleFlash(screen, hints.AimDirection, hud.RunTick);
                    }
                    if (hints.MineHeld)
                    {
                        var mining = run.LastMiningPresentation;
                        float? mineHit = mining.Active ? mining.HitDistance : 36f;
                        canvas.DrawMineRay(screen, hints.AimDirection, mineHit);
                    }

                    break;
                case CombatRenderKind.EnemyShip:
                {
                    var (region, size) = ResolveEnemySprite(item);
                    if (item.Elite)
                        canvas.DrawRegion(
                            "telegraphs/elite-marker",
                            (int)screen.X - size / 2 - 6,
                            (int)screen.Y - size / 2 - 6,
                            size + 12,
                            size + 12);
                    canvas.DrawRegionRotated(region, screen, item.Rotation, size);
                    if (item.Elite)
                        canvas.DrawText((int)screen.X - 14, (int)screen.Y + size / 2 + 2, "ELITE", new XnaColor(240, 200, 120));
                    break;
                }
                case CombatRenderKind.Projectile:
                    if (item.IsMissile)
                        canvas.DrawRegionRotated("projectiles/seeker", screen, item.Rotation, 10);
                    else
                        canvas.DrawRegion("projectiles/hostile", (int)screen.X - 3, (int)screen.Y - 3, 6, 6);
                    break;
                case CombatRenderKind.Mine:
                    canvas.DrawRegion("telegraphs/mine-radius", (int)screen.X - 10, (int)screen.Y - 10, 20, 20);
                    break;
            }
        }

        if (run.LastScoutDronePresentation.Active)
        {
            var drone = run.LastScoutDronePresentation;
            var droneScreen = canvas.WorldToScreen(drone.WorldPosition, camera);
            if (canvas.OnScreen(droneScreen, 24))
            {
                canvas.DrawRegionRotated(
                    "ships/utility/firefly",
                    droneScreen,
                    drone.Rotation,
                    18);
                if (drone.FiredThisTick)
                {
                    var aim = new System.Numerics.Vector2(MathF.Cos(drone.Rotation), MathF.Sin(drone.Rotation));
                    canvas.DrawMuzzleFlash(droneScreen, aim, hud.RunTick);
                }
            }
        }

        if (hud.Phase == RunPhase.Extraction)
        {
            var extractWorld = new System.Numerics.Vector2(
                run.Descriptor.Extraction.Center.X * FieldDescriptor.WorldUnitsPerCell,
                run.Descriptor.Extraction.Center.Y * FieldDescriptor.WorldUnitsPerCell);
            var extract = canvas.WorldToScreen(extractWorld, camera);
            if (canvas.OnScreen(extract, 40))
                canvas.DrawRegion("field/extraction-marker", (int)extract.X - 16, (int)extract.Y - 16, 32, 32);
            else if (ScreenEdgePing.Project(extract, MvpPresentation.VirtualWidth, MvpPresentation.VirtualHeight) is { } extractPing)
                canvas.DrawEdgePing("field/extraction-marker", extractPing, 24, "EXTRACT");
        }

        if (hud.Phase == RunPhase.Elite && run.TryGetEliteWorldPosition(out var eliteWorld))
        {
            var eliteScreen = canvas.WorldToScreen(eliteWorld, camera);
            if (ScreenEdgePing.Project(eliteScreen, MvpPresentation.VirtualWidth, MvpPresentation.VirtualHeight) is { } elitePing)
                canvas.DrawEdgePing("telegraphs/elite-marker", elitePing, 28, "ELITE");
        }
        else if (hud.Phase == RunPhase.Objective &&
                 !anyHostileOnScreen &&
                 run.TryGetNearestHostileWorldPosition(camera, out var hostileWorld))
        {
            var hostileScreen = canvas.WorldToScreen(hostileWorld, camera);
            if (ScreenEdgePing.Project(hostileScreen, MvpPresentation.VirtualWidth, MvpPresentation.VirtualHeight) is { } hostilePing)
                canvas.DrawEdgePing("enemies/interceptor", hostilePing, 16, "HOSTILE");
        }

        canvas.DrawParticles(camera);

        if (hints.ShowAimReticle)
            canvas.DrawAimReticle(hints.MouseVirtual);

        canvas.DrawRunFlashOverlay(hints);
        canvas.DrawRunHud(hud, hints, playerScreen);
        canvas.DrawPhaseToast();

        if (hud.Phase == RunPhase.Extraction)
            canvas.DrawText(
                8,
                76,
                $"Extracting: {hud.ExtractionProgressTicks}/{hud.ExtractionHoldTicks}",
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

    private static (string Region, int Size) ResolveEnemySprite(CombatRenderItem item)
    {
        if (item.Elite)
            return ("enemies/elite", 48);

        var archetype = item.ArchetypeId == default ? string.Empty : item.ArchetypeId.Value;
        return archetype switch
        {
            "ENM_GUNSHIP" => ("enemies/gunship", 36),
            "ENM_SAPPER" => ("enemies/sapper", 28),
            _ => ("enemies/interceptor", 22)
        };
    }
}
