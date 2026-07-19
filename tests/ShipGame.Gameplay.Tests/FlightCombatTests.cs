using System.Diagnostics;
using System.Numerics;
using ShipGame.Domain;

namespace ShipGame.Gameplay.Tests;

public sealed class FlightCombatTests
{
    private static readonly ContentId Pulse = new("MOD_WEAPON_PULSE");
    private static readonly ContentId Beam = new("MOD_WEAPON_BEAM");
    private static readonly ContentId Seeker = new("MOD_WEAPON_SEEKER");
    private static readonly ContentId Interceptor = new("ENM_INTERCEPTOR");
    private static readonly ContentId Gunship = new("ENM_GUNSHIP");
    private static readonly ContentId Sapper = new("ENM_SAPPER");

    [Fact]
    public void MovementIsFixedStepResponsiveAndFrameScheduleIndependent()
    {
        static (CombatSnapshot Moving, CombatSnapshot Braked, ulong Hash) Run(double[] frames)
        {
            var simulation = NewSimulation(Pulse);
            var accumulator = 0d;
            var frame = 0;
            while (simulation.Tick < 60)
            {
                accumulator += frames[frame++ % frames.Length];
                while (accumulator >= 1d / 60d && simulation.Tick < 60)
                {
                    // Local W stick with Aim +X → world thrust along +X (ship-relative).
                    simulation.Queue(Command(simulation.Tick, move: new Vector2(0, -1), aim: Vector2.UnitX));
                    simulation.Step();
                    accumulator -= 1d / 60d;
                }
            }
            var moving = simulation.Snapshot(simulation.Player);
            while (simulation.Tick < 90)
            {
                simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX));
                simulation.Step();
            }
            return (moving, simulation.Snapshot(simulation.Player), simulation.LastStateHash);
        }

        var sixty = Run([1d / 60d]);
        var mixed = Run([1d / 144d, 1d / 30d, 1d / 90d]);

        Assert.Equal(sixty, mixed);
        Assert.InRange(sixty.Moving.Position.X, 190, 220);
        Assert.InRange(sixty.Braked.Position.X - sixty.Moving.Position.X, 15, 25);
    }

    [Fact]
    public void PlayerThrustFollowsAimFacingNotWorldAxes()
    {
        var alongX = NewSimulation(Pulse);
        for (var i = 0; i < 45; i++)
        {
            alongX.Queue(Command(alongX.Tick, move: new Vector2(0, -1), aim: Vector2.UnitX));
            alongX.Step();
        }
        var xPos = alongX.Snapshot(alongX.Player).Position;
        Assert.True(xPos.X > 80, $"Expected forward thrust along +X, got {xPos}");
        Assert.InRange(xPos.Y, -8f, 8f);

        var alongY = NewSimulation(Pulse);
        for (var i = 0; i < 45; i++)
        {
            alongY.Queue(Command(alongY.Tick, move: new Vector2(0, -1), aim: Vector2.UnitY));
            alongY.Step();
        }
        var yPos = alongY.Snapshot(alongY.Player).Position;
        Assert.True(yPos.Y > 80, $"Expected forward thrust along +Y, got {yPos}");
        Assert.InRange(yPos.X, -8f, 8f);
    }

    [Fact]
    public void ShieldAbsorbsFirstSpillsToHullAndRechargesOnExactBoundary()
    {
        var simulation = NewSimulation(Pulse);
        var source = simulation.SpawnEnemy(Interceptor, new Vector2(500, 0));
        simulation.InflictDamage(simulation.Player, source, 70);

        simulation.Step();

        var damaged = simulation.Snapshot(simulation.Player);
        Assert.Equal(0, damaged.Shield);
        Assert.Equal(90, damaged.Hull);
        Assert.Contains(simulation.Events, value => value.Kind == CombatEventKind.ShieldDepleted);
        for (var i = 0; i < 179; i++)
            simulation.Step();
        Assert.Equal(0, simulation.Snapshot(simulation.Player).Shield);
        simulation.Step();
        Assert.Equal(0.2f, simulation.Snapshot(simulation.Player).Shield, 3);
    }

    [Fact]
    public void SimultaneousDamageUsesStableSourceOrderAndAllHitsApply()
    {
        var simulation = NewSimulation(Pulse);
        var first = simulation.SpawnEnemy(Interceptor, new Vector2(600, 0));
        var second = simulation.SpawnEnemy(Gunship, new Vector2(700, 0));
        simulation.InflictDamage(simulation.Player, second, 35);
        simulation.InflictDamage(simulation.Player, first, 35);

        simulation.Step();

        Assert.Equal(90, simulation.Snapshot(simulation.Player).Hull);
        var orderedSources = simulation.Events
            .Where(value => value.Kind is CombatEventKind.ShieldDamaged or CombatEventKind.HullDamaged)
            .Select(value => value.Other)
            .ToArray();
        Assert.Equal([first, second, second], orderedSources);
    }

    [Fact]
    public void PulseHasExactFivePerSecondCadence()
    {
        var simulation = NewSimulation(Pulse);
        var firedTicks = new List<long>();
        for (var tick = 0; tick <= 60; tick++)
        {
            simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
            simulation.Step();
            firedTicks.AddRange(simulation.Events
                .Where(value => value.Kind == CombatEventKind.WeaponFired)
                .Select(value => value.Tick));
        }

        Assert.Equal([0L, 12L, 24L, 36L, 48L, 60L], firedTicks);
    }

    [Fact]
    public void BeamOverheatsAfterSixSecondsAndVentsWhileTriggerHeld()
    {
        var simulation = NewSimulation(Beam);
        // HeatPerTick 0.5 → 360 ticks to reach 180.
        for (var tick = 0; tick < 360; tick++)
        {
            simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
            simulation.Step();
        }

        Assert.True(simulation.WeaponStatus(simulation.Player).HeatLocked);
        Assert.Equal(180, simulation.WeaponStatus(simulation.Player).Heat);

        // CoolPerTick 3 while locked, even with Fire held.
        for (var tick = 0; tick < 59; tick++)
        {
            simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
            simulation.Step();
        }

        Assert.True(simulation.WeaponStatus(simulation.Player).HeatLocked);
        simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
        simulation.Step();
        Assert.False(simulation.WeaponStatus(simulation.Player).HeatLocked);
        Assert.Equal(0, simulation.WeaponStatus(simulation.Player).Heat);
    }

    [Fact]
    public void BeamDamagesEnemyAimedAtCenter()
    {
        var simulation = NewSimulation(Beam);
        var target = simulation.SpawnEnemy(Interceptor, new Vector2(200, 0));
        var beforeHull = simulation.Snapshot(target).Hull;
        var fired = false;
        var destroyed = false;
        float? afterHull = null;
        for (var i = 0; i < 30; i++)
        {
            simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
            simulation.Step();
            fired |= simulation.Events.Any(value => value.Kind == CombatEventKind.WeaponFired && value.Other == target);
            destroyed |= simulation.Events.Any(value =>
                value.Kind == CombatEventKind.EntityDestroyed && value.Entity == target);
            if (!destroyed)
                afterHull = simulation.Snapshot(target).Hull;
        }

        Assert.True(fired, "Expected WeaponFired against the aimed target.");
        Assert.True(destroyed || afterHull < beforeHull, "Expected beam damage on-center.");
    }

    [Fact]
    public void BeamKillsInterceptorQuicklyWhenHeldOnTarget()
    {
        var simulation = NewSimulation(Beam);
        var target = simulation.SpawnEnemy(Interceptor, new Vector2(180, 0));
        for (var i = 0; i < 45; i++)
        {
            simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
            simulation.Step();
            if (simulation.Events.Any(value => value.Kind == CombatEventKind.EntityDestroyed && value.Entity == target))
                return;
        }

        Assert.Fail("Beam should delete a 28-hull interceptor within ~0.75s on target.");
    }

    [Fact]
    public void BeamDamagesEnemyWhenAimSkimsCollider()
    {
        var simulation = NewSimulation(Beam);
        // Interceptor collider radius 16; offset Y=12 keeps center outside a tight cone but ray clips body.
        var target = simulation.SpawnEnemy(Interceptor, new Vector2(200, 12));
        var beforeHull = simulation.Snapshot(target).Hull;
        var destroyed = false;
        float? afterHull = null;
        for (var i = 0; i < 30; i++)
        {
            simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
            simulation.Step();
            destroyed |= simulation.Events.Any(value =>
                value.Kind == CombatEventKind.EntityDestroyed && value.Entity == target);
            if (!destroyed)
                afterHull = simulation.Snapshot(target).Hull;
        }

        Assert.True(destroyed || afterHull < beforeHull, "Expected collider-aware beam hit.");
    }

    [Fact]
    public void BeamDoesNotDamageFarOffAngleEnemy()
    {
        var simulation = NewSimulation(Beam);
        var target = simulation.SpawnEnemy(Interceptor, new Vector2(200, 180));
        var before = simulation.Snapshot(target);
        var firedAtTarget = false;
        for (var i = 0; i < 30; i++)
        {
            simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
            simulation.Step();
            firedAtTarget |= simulation.Events.Any(value =>
                value.Kind == CombatEventKind.WeaponFired && value.Other == target);
        }

        var after = simulation.Snapshot(target);
        Assert.False(firedAtTarget);
        Assert.Equal(before.Hull, after.Hull);
        Assert.False(after.Destroyed);
    }

    [Fact]
    public void BeamHitDistanceUsesAimRaySurfaceNotCenter()
    {
        var simulation = NewSimulation(Beam);
        // Interceptor collider radius 16; on-axis lock should report surface entry (~184), not 200.
        simulation.SpawnEnemy(Interceptor, new Vector2(200, 0));
        simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
        simulation.Step();

        Assert.True(simulation.TryGetPlayerBeamHitDistance(out var hit));
        Assert.InRange(hit, 183.5f, 184.5f);
    }

    [Fact]
    public void BeamHitDistanceIsAbsentForConeOnlyLock()
    {
        var simulation = NewSimulation(Beam);
        // Y=30 is inside the 24° cone but outside the r=16 collider along +X aim.
        simulation.SpawnEnemy(Interceptor, new Vector2(200, 30));
        simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
        simulation.Step();

        Assert.Contains(simulation.Events, value => value.Kind == CombatEventKind.WeaponFired);
        Assert.False(simulation.TryGetPlayerBeamHitDistance(out _));
    }

    [Fact]
    public void DestroyEntityRemovesObstacleWithoutCombatKillEvent()
    {
        var simulation = NewSimulation(Pulse);
        var obstacle = simulation.SpawnObstacle(new Vector2(100, 0), 12f);
        Assert.False(simulation.Snapshot(obstacle).Destroyed);

        simulation.DestroyEntity(obstacle);
        Assert.True(simulation.Snapshot(obstacle).Destroyed);
        Assert.DoesNotContain(simulation.Events, value => value.Kind == CombatEventKind.EntityDestroyed);

        simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX));
        simulation.Step();
        Assert.Throws<InvalidOperationException>(() => simulation.Snapshot(obstacle));
    }

    [Fact]
    public void SeekerFiresStraightWithoutLockAndHomesWithConeLock()
    {
        var simulation = NewSimulation(Seeker);
        var target = simulation.SpawnEnemy(Interceptor, new Vector2(300, 0));
        // Off-cone aim still free-fires straight (no lock target on the event).
        simulation.Queue(Command(0, aim: Vector2.UnitY, actions: FlightAction.Fire));
        simulation.Step();
        Assert.Contains(simulation.Events, value =>
            value.Kind == CombatEventKind.WeaponFired && value.Other == default && value.Amount == 2);

        // Wait out seeker cadence (~36 ticks) then lock on-axis.
        for (var i = 1; i < 40; i++)
            simulation.Step();
        simulation.Queue(Command(simulation.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
        simulation.Step();
        Assert.Contains(simulation.Events, value =>
            value.Kind == CombatEventKind.WeaponFired && value.Other == target && value.Amount == 2);

        simulation.InflictDamage(target, simulation.Player, 100);
        simulation.Step();
        var exception = Record.Exception(() =>
        {
            for (var i = 0; i < 30; i++)
                simulation.Step();
        });
        Assert.Null(exception);
    }

    [Fact]
    public void SeekerMissileRenderRotationTracksVelocityHeading()
    {
        var simulation = NewSimulation(Seeker);
        // Straight free-fire along +Y (no lock): rotation must stay on that heading.
        simulation.Queue(Command(0, aim: Vector2.UnitY, actions: FlightAction.Fire));
        simulation.Step();

        var items = new List<CombatRenderItem>(32);
        simulation.CollectRenderItems(items);
        var missiles = items.Where(item => item.Kind == CombatRenderKind.Projectile && item.IsMissile).ToList();
        Assert.NotEmpty(missiles);
        foreach (var missile in missiles)
            Assert.InRange(WrapPi(missile.Rotation - MathF.PI / 2f), -0.05f, 0.05f);

        var tracked = missiles[0];
        var previous = tracked.Position;
        var samples = 0;
        for (var i = 0; i < 20; i++)
        {
            simulation.Step();
            items.Clear();
            simulation.CollectRenderItems(items);
            var current = items.FirstOrDefault(item => item.Entity == tracked.Entity);
            if (current.Entity == default)
                break;
            var delta = current.Position - previous;
            if (delta.LengthSquared() > 0.0001f)
            {
                var heading = MathF.Atan2(delta.Y, delta.X);
                Assert.InRange(WrapPi(current.Rotation - heading), -0.05f, 0.05f);
                samples++;
            }

            previous = current.Position;
            tracked = current;
        }

        Assert.True(samples > 0);

        // Homing path: lock a target off the launch axis and keep rotation on velocity.
        var target = simulation.SpawnEnemy(Interceptor, new Vector2(240, 120));
        for (var i = 0; i < 40; i++)
            simulation.Step();
        var aim = Vector2.Normalize(new Vector2(240, 120));
        simulation.Queue(Command(simulation.Tick, aim: aim, actions: FlightAction.Fire));
        simulation.Step();
        Assert.Contains(simulation.Events, value =>
            value.Kind == CombatEventKind.WeaponFired && value.Other == target);

        items.Clear();
        simulation.CollectRenderItems(items);
        missiles = items.Where(item => item.Kind == CombatRenderKind.Projectile && item.IsMissile).ToList();
        Assert.NotEmpty(missiles);
        tracked = missiles[0];
        previous = tracked.Position;
        samples = 0;
        for (var i = 0; i < 40; i++)
        {
            simulation.Step();
            items.Clear();
            simulation.CollectRenderItems(items);
            var current = items.FirstOrDefault(item => item.Entity == tracked.Entity);
            if (current.Entity == default)
                break;
            var delta = current.Position - previous;
            if (delta.LengthSquared() > 0.0001f)
            {
                Assert.InRange(WrapPi(current.Rotation - MathF.Atan2(delta.Y, delta.X)), -0.05f, 0.05f);
                samples++;
            }

            previous = current.Position;
            tracked = current;
        }

        Assert.True(samples > 0);
    }

    private static float WrapPi(float radians)
    {
        while (radians > MathF.PI)
            radians -= MathF.Tau;
        while (radians < -MathF.PI)
            radians += MathF.Tau;
        return radians;
    }

    [Theory]
    [InlineData(MobilityBehavior.Dash, 240)]
    [InlineData(MobilityBehavior.Blink, 360)]
    public void MobilityShortensAtObstructionAndEnforcesCooldown(MobilityBehavior behavior, int cooldown)
    {
        var simulation = NewSimulation(Pulse, behavior);
        simulation.SpawnObstacle(new Vector2(100, 0), 20);
        simulation.Queue(Command(0, move: new Vector2(0, -1), aim: Vector2.UnitX, actions: FlightAction.Mobility));

        simulation.Step();

        Assert.InRange(simulation.Snapshot(simulation.Player).Position.X, 61.9f, 62.1f);
        Assert.Equal(cooldown, simulation.MobilityStatus(simulation.Player).CooldownRemaining);
        simulation.Queue(Command(1, move: new Vector2(0, -1), aim: Vector2.UnitX, actions: FlightAction.None));
        simulation.Step();
        simulation.Queue(Command(2, move: new Vector2(0, -1), aim: Vector2.UnitX, actions: FlightAction.Mobility));
        simulation.Step();
        Assert.Contains(simulation.Events, value =>
            value.Kind == CombatEventKind.AbilityRejected && value.Detail == "cooldown");
    }

    [Fact]
    public void DashGrantsInvulnerabilityWindow()
    {
        var simulation = NewSimulation(Pulse);
        var source = simulation.SpawnEnemy(Interceptor, new Vector2(400, 0));
        simulation.Queue(Command(0, move: new Vector2(0, -1), aim: Vector2.UnitX, actions: FlightAction.Mobility));
        simulation.Step();
        Assert.Contains(simulation.Events, value => value.Kind == CombatEventKind.AbilityActivated);

        var before = simulation.Snapshot(simulation.Player);
        simulation.InflictDamage(simulation.Player, source, 40);
        simulation.Step();
        var after = simulation.Snapshot(simulation.Player);
        Assert.Equal(before.Shield, after.Shield);
        Assert.Equal(before.Hull, after.Hull);
    }

    [Fact]
    public void TemporaryModifierGrantIsConsumedOnceAndClearsAtRunBoundary()
    {
        var simulation = NewSimulation(Pulse);
        var target = simulation.SpawnEnemy(Interceptor, new Vector2(80, 0));
        simulation.GrantTemporaryModifiers(new TemporaryCombatModifiers(2, 1, 1, 1, 0, 0.6f, 0, false));
        simulation.Step();
        Assert.Equal(2, simulation.TemporaryModifiers(simulation.Player).DamageMultiplier);
        simulation.Queue(Command(1, aim: Vector2.UnitX, actions: FlightAction.Fire));
        for (var i = 0; i < 10; i++)
            simulation.Step();
        Assert.Equal(8, simulation.Snapshot(target).Hull);

        simulation.ClearTemporaryModifiers();
        Assert.Equal(1, simulation.TemporaryModifiers(simulation.Player).DamageMultiplier);
    }

    [Theory]
    [InlineData("ENM_INTERCEPTOR")]
    [InlineData("ENM_GUNSHIP")]
    [InlineData("ENM_SAPPER")]
    public void EveryEnemyBehaviorActsDeterministically(string id)
    {
        static (ulong Hash, bool Acted) Run(string enemyId)
        {
            var simulation = NewSimulation(Pulse);
            simulation.SpawnEnemy(new ContentId(enemyId), new Vector2(250, 0), elite: true);
            var acted = false;
            for (var i = 0; i < 240; i++)
            {
                simulation.Step();
                acted |= simulation.Events.Any(value =>
                    value.Kind is CombatEventKind.MineTelegraphed or CombatEventKind.HullDamaged
                        or CombatEventKind.ShieldDamaged);
            }
            return (simulation.LastStateHash, acted);
        }

        var first = Run(id);
        Assert.Equal(first, Run(id));
        Assert.True(first.Acted);
    }

    [Fact]
    public void EliteProtocolIsOncePerRunAndEmitsActivation()
    {
        var simulation = NewSimulation(Pulse);
        var elite = simulation.SpawnEnemy(Interceptor, new Vector2(200, 0), elite: true);
        Assert.Contains(simulation.Events, value =>
            value.Kind == CombatEventKind.EliteActivated &&
            value.Entity == elite &&
            value.ContentId.Value == "MOD_ELITE_PROTOCOL");
        Assert.Throws<InvalidOperationException>(() =>
            simulation.SpawnEnemy(Gunship, new Vector2(220, 0), elite: true));
        Assert.Equal(28 * 2.75f, simulation.Snapshot(elite).Hull);
    }

    [Fact]
    public void ThreatDirectorUsesValidDistantAnchorsAndHonorsCap()
    {
        var simulation = NewSimulation(Pulse);
        simulation.AddSpawnAnchor(new Vector2(100, 0));
        simulation.AddSpawnAnchor(new Vector2(500, 0));
        simulation.ConfigureThreatDirector(1, 1);

        simulation.Step();
        simulation.Step();

        var spawned = Assert.Single(simulation.Events, value => value.Kind == CombatEventKind.EnemySpawned);
        Assert.Equal(500, simulation.Snapshot(spawned.Entity).Position.X);
        simulation.Step();
        Assert.DoesNotContain(simulation.Events, value => value.Kind == CombatEventKind.EnemySpawned);
    }

    [Fact]
    public void SameSpawnOrderIsDeterministicAcrossRuns()
    {
        static ulong Run(bool reverse)
        {
            var simulation = NewSimulation(Pulse);
            if (reverse)
            {
                simulation.SpawnEnemy(Gunship, new Vector2(300, 50));
                simulation.SpawnEnemy(Interceptor, new Vector2(300, -50));
            }
            else
            {
                simulation.SpawnEnemy(Interceptor, new Vector2(300, -50));
                simulation.SpawnEnemy(Gunship, new Vector2(300, 50));
            }
            for (var tick = 0; tick < 120; tick++)
            {
                simulation.Queue(Command(simulation.Tick, move: Vector2.UnitY, aim: Vector2.UnitX, actions: FlightAction.Fire));
                simulation.Step();
            }
            return simulation.LastStateHash;
        }

        Assert.Equal(Run(false), Run(false));
        Assert.Equal(Run(true), Run(true));
        // Creation-ordered entity IDs make reverse spawn order a different universe; not an insertion-stable claim.
        Assert.NotEqual(Run(false), Run(true));
    }

    [Fact]
    public void StaleAndFutureCommandsAreRejected()
    {
        var simulation = NewSimulation(Pulse);
        simulation.Step();

        Assert.False(simulation.Queue(Command(simulation.Tick - 1)));
        Assert.Contains(simulation.Events, value =>
            value.Kind == CombatEventKind.CommandRejected && value.Detail == "stale");

        Assert.False(simulation.Queue(Command(simulation.Tick + FlightCombatConstants.CommandHorizonTicks + 1)));
        Assert.Contains(simulation.Events, value =>
            value.Kind == CombatEventKind.CommandRejected && value.Detail == "future");
    }

    [Fact]
    public void PendingCommandsAreIncludedInAuthoritativeHash()
    {
        var baseline = NewSimulation(Pulse);
        var modified = NewSimulation(Pulse);
        baseline.Step();
        modified.Step();
        Assert.Equal(baseline.LastStateHash, modified.LastStateHash);

        Assert.True(modified.Queue(Command(modified.Tick + 5, actions: FlightAction.Fire)));
        Assert.NotEqual(baseline.LastStateHash, modified.LastStateHash);

        baseline.Step();
        modified.Step();
        Assert.NotEqual(baseline.LastStateHash, modified.LastStateHash);
    }

    [Fact]
    public void StaleEntityDamageAndSnapshotRemainSafe()
    {
        var simulation = NewSimulation(Pulse);
        var enemy = simulation.SpawnEnemy(Interceptor, new Vector2(40, 0));
        simulation.InflictDamage(enemy, simulation.Player, 1_000);
        simulation.Step();
        simulation.Step();

        Assert.Throws<InvalidOperationException>(() => simulation.Snapshot(enemy));
        Assert.Null(Record.Exception(() =>
        {
            simulation.InflictDamage(enemy, simulation.Player, 10);
            simulation.InflictDamage(simulation.Player, enemy, 10);
            simulation.Step();
        }));
    }

    [Fact]
    public void WarmIdleAndQueuedCombatHaveZeroSteadyStateAllocation()
    {
        var idle = NewSimulation(Pulse);
        for (var i = 0; i < 2_000; i++)
            idle.Step();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        for (var i = 0; i < 200; i++)
            idle.Step();

        var stopwatch = Stopwatch.StartNew();
        var beforeIdle = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20_000; i++)
            idle.Step();
        var idleAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeIdle;
        stopwatch.Stop();

        Assert.Equal(0, idleAllocated);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"20,000 fixed ticks took {stopwatch.Elapsed.TotalMilliseconds:F1} ms.");

        // Continuous Queue+Step under combat stepping (movement/aim ingress + obstacle contact work).
        // AI/weapon spawn paths are excluded here; they create entities and are documented below.
        var queued = NewSimulation(Pulse);
        queued.SpawnObstacle(new Vector2(80, 0), 16);
        for (var i = 0; i < 2_000; i++)
        {
            queued.Queue(Command(queued.Tick, move: new Vector2(0, -1), aim: Vector2.UnitX));
            queued.Step();
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        for (var i = 0; i < 200; i++)
        {
            queued.Queue(Command(queued.Tick, move: new Vector2(0, -1), aim: Vector2.UnitX));
            queued.Step();
        }

        var beforeQueued = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 5_000; i++)
        {
            queued.Queue(Command(queued.Tick, move: new Vector2(0, -1), aim: Vector2.UnitX));
            queued.Step();
        }
        var queuedAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeQueued;
        Assert.Equal(0, queuedAllocated);

        // Unavoidable transient: projectile entity creation allocates. Not per-tick command growth —
        // identical Queue+Step without Fire stays 0 B; enabling Fire allocates only when projectiles spawn.
        var firing = NewSimulation(Pulse);
        for (var i = 0; i < 120; i++)
        {
            firing.Queue(Command(firing.Tick, aim: Vector2.UnitX));
            firing.Step();
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var beforeNoFire = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++)
        {
            firing.Queue(Command(firing.Tick, aim: Vector2.UnitX));
            firing.Step();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - beforeNoFire);

        var beforeFire = GC.GetAllocatedBytesForCurrentThread();
        var fired = 0;
        for (var i = 0; i < 60; i++)
        {
            firing.Queue(Command(firing.Tick, aim: Vector2.UnitX, actions: FlightAction.Fire));
            firing.Step();
            fired += firing.Events.Count(value => value.Kind == CombatEventKind.WeaponFired);
        }
        var fireAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeFire;
        Assert.True(fired > 0);
        Assert.True(fireAllocated > 0,
            "Expected entity-spawn allocations while projectiles are created.");
    }

    [Fact]
    public void PackageScheduleIsExactOrderedProjectionOfStep()
    {
        var simulation = NewSimulation(Pulse);
        // Canonical Step() phase order. Step only invokes SystemScheduler.Tick for this list;
        // changing either registration order or this expectation fails the binding.
        Assert.Equal(
            [
                "ApplyFlightCombatStructuralChanges",
                "ConsumeFlightCommands",
                "AdvanceCombatTimers",
                "ConsumeTemporaryModifiers",
                "AiAndThreatDecisions",
                "ResolveMobility",
                "IntegrateFlightMovement",
                "RebuildCombatSpatialIndex",
                "DetectCombatCollisions",
                "ResolveWeapons",
                "ResolveMines",
                "ResolveOrderedDamage",
                "PublishCombatEventsAndHash"
            ],
            simulation.Schedule);
        Assert.Equal(
            ["ConsumeCommands", "SessionTransitions", "RunClock", "PublishAndHash"],
            new FoundationSession(1).Schedule);
    }

    private static FlightCombatWorld NewSimulation(
        ContentId weapon,
        MobilityBehavior mobility = MobilityBehavior.Dash)
    {
        var simulation = new FlightCombatWorld(42);
        simulation.SpawnPlayer(Vector2.Zero, weapon, mobility);
        return simulation;
    }

    private static FlightCommandFrame Command(
        long tick,
        Vector2 move = default,
        Vector2 aim = default,
        FlightAction actions = FlightAction.None) =>
        new(
            tick,
            FlightCommandFrame.Quantize(move.X),
            FlightCommandFrame.Quantize(move.Y),
            FlightCommandFrame.Quantize(aim.X),
            FlightCommandFrame.Quantize(aim.Y),
            actions);
}
