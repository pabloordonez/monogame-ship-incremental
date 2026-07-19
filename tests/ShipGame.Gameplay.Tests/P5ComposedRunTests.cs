using System.Numerics;
using ShipGame.Domain;
using ShipGame.Gameplay;

namespace ShipGame.Gameplay.Tests;

public sealed class P5ComposedRunTests
{
    [Fact]
    public void GoldenTrace_FlightDashPauseAndReturn_ReachesCheckpoints()
    {
        var run = CreateRun(1);
        Assert.Contains("run_started", run.Checkpoints);
        run.Queue(new FlightCommandFrame(run.Combat.Tick, FlightCommandFrame.Quantize(1), 0, FlightCommandFrame.Quantize(1), 0, FlightAction.None));
        run.Step();
        run.Queue(new FlightCommandFrame(run.Combat.Tick, 0, 0, FlightCommandFrame.Quantize(1), 0, FlightAction.Mobility));
        run.Step();
        run.SetPaused(true);
        var tick = run.WorldRun.RunTick;
        run.Step(FlightCommandFrame.Neutral(run.Combat.Tick));
        Assert.Equal(tick, run.WorldRun.RunTick);
        run.SetPaused(false);
        run.Step(FlightCommandFrame.Neutral(run.Combat.Tick));
        Assert.True(run.WorldRun.RunTick > tick);
        Assert.Equal(ComposedRunStatus.Active, run.Status);
    }

    [Fact]
    public void GoldenTrace_WeaponsAgainstArchetypes_AreDeterministic()
    {
        var left = CreateCombatProbe(11);
        var right = CreateCombatProbe(11);
        Assert.Equal(left, right);
        Assert.True(left > 0);
    }

    [Fact]
    public void GoldenTrace_MineUpgradesExtract_ReachesExpectedCheckpoints()
    {
        var run = CreateRun(21);
        var reward = run.CompleteViaHarness(succeed: true);
        Assert.True(reward.Succeeded);
        Assert.Contains("objective_complete", run.Checkpoints);
        Assert.Contains("elite_defeated", run.Checkpoints);
        Assert.Contains("extraction_ready", run.Checkpoints);
        Assert.Contains("extracted", run.Checkpoints);
        Assert.Contains("reward_mapped", run.Checkpoints);
        Assert.Equal(RunOutcome.Success, run.WorldRun.Reward!.Outcome);
        Assert.Contains("upgrade_applied", run.Checkpoints);
    }

    [Fact]
    public void GoldenTrace_FailByHullAndDeadline()
    {
        var hull = CreateRun(31);
        var hullReward = hull.CompleteViaHarness(succeed: false);
        Assert.False(hullReward.Succeeded);
        Assert.Equal(RunOutcome.HullFailure, hull.WorldRun.Reward!.Outcome);
        Assert.Contains("run_failed", hull.Checkpoints);

        var deadline = CreateRun(32);
        var deadlineReward = deadline.FailByDeadlineHarness();
        Assert.False(deadlineReward.Succeeded);
        Assert.Equal(RunOutcome.DeadlineFailure, deadline.WorldRun.Reward!.Outcome);
        Assert.Contains("deadline_failure", deadline.Checkpoints);
    }

    [Fact]
    public void GoldenTrace_RewardsResearchEquipSaveContinue()
    {
        var profile = ProfileAggregate.CreateNew(41);
        Assert.Equal(ProfileMutationStatus.Applied, profile.BeginRun("TX_BEGIN_1").Status);
        var run = new ComposedRunOrchestrator(
            WorldRunIds.CinderBelt,
            profile.Snapshot.ProfileSeed,
            profile.Snapshot.RunIndex,
            profile.ResolveLoadout(),
            profile.DeriveStatistics(),
            recoveryProtocols: false);
        var reward = run.CompleteViaHarness(succeed: true);
        Assert.Equal(ProfileMutationStatus.Applied, profile.CommitAcceptedReward(reward).Status);
        Assert.Equal(ProfileMutationStatus.Applied, profile.PurchaseResearch("TX_HULL", ResearchCatalog.HullReinforcement).Status);
        Assert.Equal(
            ProfileMutationStatus.Applied,
            profile.EquipModule("TX_EQUIP", ModuleSlot.Weapon, ModuleCatalog.WeaponPulse).Status);

        var continued = new ProfileAggregate(profile.Snapshot);
        Assert.Contains(ResearchCatalog.HullReinforcement, continued.Snapshot.PurchasedResearchIds);
        Assert.Equal(ModuleCatalog.WeaponPulse, continued.Snapshot.RequestedLoadout.Weapon);
        Assert.True(continued.Snapshot.Balances.Ferrite >= 0);
        Assert.Equal(1, continued.Snapshot.RunIndex);
    }

    [Fact]
    public void SameSeedComposedHarness_IsDeterministicAcrossSchedules()
    {
        var a = CreateRun(77).CompleteViaHarness();
        var b = CreateRun(77).CompleteViaHarness();
        Assert.Equal(a.Earned, b.Earned);
        Assert.Equal(a.Banked, b.Banked);
        Assert.Equal(a.TransactionId, b.TransactionId);
    }

    [Fact]
    public void LiveMineAction_BreaksResourceCellWithoutHarnessFacts()
    {
        var profile = ProfileAggregate.CreateNew(88);
        profile.BeginRun("TX_BEGIN_MINE");
        var run = new ComposedRunOrchestrator(
            WorldRunIds.CinderBelt,
            profile.Snapshot.ProfileSeed,
            profile.Snapshot.RunIndex,
            profile.ResolveLoadout(),
            profile.DeriveStatistics(),
            recoveryProtocols: false,
            enableThreatDirector: false);
        var playerStart = run.Combat.Snapshot(run.Combat.Player).Position;
        var target = run.Asteroids
            .Where(a => a.Kind == AsteroidCellKind.Ferrite && !a.Broken)
            .OrderBy(a => Dist2(playerStart, a.X, a.Y))
            .First();

        for (var i = 0; i < 2_400; i++)
        {
            var player = run.Combat.Snapshot(run.Combat.Player).Position;
            var delta = new Vector2(target.X - player.X, target.Y - player.Y);
            if (delta.Length() <= ComposedRunOrchestrator.MiningRangeWorldUnits * 0.8f)
                break;
            var dir = Vector2.Normalize(delta);
            run.Step(new FlightCommandFrame(
                run.Combat.Tick,
                FlightCommandFrame.Quantize(dir.X),
                FlightCommandFrame.Quantize(dir.Y),
                FlightCommandFrame.Quantize(dir.X),
                FlightCommandFrame.Quantize(dir.Y),
                FlightAction.None));
            Assert.NotEqual(ComposedRunStatus.Terminal, run.Status);
        }

        var brokenBefore = run.Asteroids.Count(a => a.Broken);
        for (var i = 0; i < 1_200; i++)
        {
            var player = run.Combat.Snapshot(run.Combat.Player).Position;
            var delta = new Vector2(target.X - player.X, target.Y - player.Y);
            var dir = delta.LengthSquared() < 0.001f ? Vector2.UnitX : Vector2.Normalize(delta);
            run.Step(new FlightCommandFrame(
                run.Combat.Tick,
                FlightCommandFrame.Quantize(dir.X * 0.2f),
                FlightCommandFrame.Quantize(dir.Y * 0.2f),
                FlightCommandFrame.Quantize(dir.X),
                FlightCommandFrame.Quantize(dir.Y),
                FlightAction.Mine));
            if (run.Asteroids.Count(a => a.Broken) > brokenBefore || run.Pickups.Any() || run.Hud.FerriteHeld > 0)
                break;
        }

        Assert.True(
            run.Asteroids.Count(a => a.Broken) > brokenBefore ||
            run.Pickups.Any() ||
            run.Hud.FerriteHeld > 0,
            "Live Mine action should damage/break a resource cell or spawn/collect loot.");
    }

    [Fact]
    public void MiningBreakRemovesMirroredCombatObstacle()
    {
        var profile = ProfileAggregate.CreateNew(91);
        profile.BeginRun("TX_BEGIN_OBSTACLE");
        var run = new ComposedRunOrchestrator(
            WorldRunIds.CinderBelt,
            profile.Snapshot.ProfileSeed,
            profile.Snapshot.RunIndex,
            profile.ResolveLoadout(),
            profile.DeriveStatistics(),
            recoveryProtocols: false,
            enableThreatDirector: false);

        var liveAsteroids = run.Asteroids.Count(a => !a.Broken);
        Assert.Equal(liveAsteroids, CountLiveNeutralObstacles(run));

        var playerStart = run.Combat.Snapshot(run.Combat.Player).Position;
        var target = run.Asteroids
            .Where(a => !a.Broken)
            .OrderBy(a => Dist2(playerStart, a.X, a.Y))
            .First();
        Assert.True(HasLiveNeutralNear(run, target.X, target.Y));

        for (var i = 0; i < 2_400; i++)
        {
            var player = run.Combat.Snapshot(run.Combat.Player).Position;
            var delta = new Vector2(target.X - player.X, target.Y - player.Y);
            if (delta.Length() <= ComposedRunOrchestrator.MiningRangeWorldUnits * 0.8f)
                break;
            var dir = Vector2.Normalize(delta);
            run.Step(new FlightCommandFrame(
                run.Combat.Tick,
                FlightCommandFrame.Quantize(dir.X),
                FlightCommandFrame.Quantize(dir.Y),
                FlightCommandFrame.Quantize(dir.X),
                FlightCommandFrame.Quantize(dir.Y),
                FlightAction.None));
        }

        for (var i = 0; i < 1_200; i++)
        {
            var player = run.Combat.Snapshot(run.Combat.Player).Position;
            var delta = new Vector2(target.X - player.X, target.Y - player.Y);
            var dir = delta.LengthSquared() < 0.001f ? Vector2.UnitX : Vector2.Normalize(delta);
            run.Step(new FlightCommandFrame(
                run.Combat.Tick,
                FlightCommandFrame.Quantize(dir.X * 0.2f),
                FlightCommandFrame.Quantize(dir.Y * 0.2f),
                FlightCommandFrame.Quantize(dir.X),
                FlightCommandFrame.Quantize(dir.Y),
                FlightAction.Mine));
            if (run.Asteroids.Any(a => a.CellId == target.CellId && a.Broken))
                break;
        }

        Assert.Contains(run.Asteroids, a => a.CellId == target.CellId && a.Broken);

        // Structural removal of Destroyed obstacles happens at the start of the next combat step.
        run.Step(FlightCommandFrame.Neutral(run.Combat.Tick));
        Assert.False(HasLiveNeutralNear(run, target.X, target.Y));
        Assert.Equal(run.Asteroids.Count(a => !a.Broken), CountLiveNeutralObstacles(run));
    }

    private static float Dist2(Vector2 origin, int x, int y)
    {
        var dx = origin.X - x;
        var dy = origin.Y - y;
        return dx * dx + dy * dy;
    }

    private static int CountLiveNeutralObstacles(ComposedRunOrchestrator run) =>
        run.LiveCombatSnapshots.Count(s => s.Faction == Faction.Neutral && !s.Destroyed);

    private static bool HasLiveNeutralNear(ComposedRunOrchestrator run, int x, int y) =>
        run.LiveCombatSnapshots.Any(s =>
            s.Faction == Faction.Neutral &&
            !s.Destroyed &&
            Dist2(s.Position, x, y) <= 1f);

    [Fact]
    public void ReliabilityProbe_TenHarnessExtracts_NoDuplicateRewardCorruption()
    {
        for (var i = 0; i < 10; i++)
        {
            var profile = ProfileAggregate.CreateNew(1000UL + (ulong)i);
            Assert.Equal(ProfileMutationStatus.Applied, profile.BeginRun($"TX_BEGIN_REL_{i}").Status);
            var run = new ComposedRunOrchestrator(
                WorldRunIds.CinderBelt,
                profile.Snapshot.ProfileSeed,
                profile.Snapshot.RunIndex,
                profile.ResolveLoadout(),
                profile.DeriveStatistics(),
                recoveryProtocols: false);
            var reward = run.CompleteViaHarness(succeed: true);
            Assert.Equal(ProfileMutationStatus.Applied, profile.CommitAcceptedReward(reward).Status);
            Assert.Equal(ProfileMutationStatus.Duplicate, profile.CommitAcceptedReward(reward).Status);
            Assert.Equal(1, profile.Snapshot.Counters.Extractions);
        }
    }

    [Fact]
    public void TractorLoadout_KeepsExpandedCollectionRadiusAcrossTicks()
    {
        var profile = ProfileAggregate.CreateNew(501);
        // Tractor is the default utility; radius should stay above the base collector.
        Assert.Equal(ModuleCatalog.UtilityTractor, profile.ResolveLoadout().Effective.Utility);
        profile.BeginRun("TX_BEGIN_TRACTOR");
        var stats = profile.DeriveStatistics();
        Assert.True(stats.PickupRadius > ComposedRunOrchestrator.BaseCollectionRadius);

        var run = new ComposedRunOrchestrator(
            WorldRunIds.CinderBelt,
            profile.Snapshot.ProfileSeed,
            profile.Snapshot.RunIndex,
            profile.ResolveLoadout(),
            stats,
            recoveryProtocols: false);
        Assert.Equal(stats.PickupRadius, run.ActiveCollectionRadius);

        for (var i = 0; i < 30; i++)
            run.Step(FlightCommandFrame.Neutral(run.Combat.Tick));

        Assert.Equal(stats.PickupRadius, run.ActiveCollectionRadius);
        Assert.True(run.ActivePullSpeedPerTick > ComposedRunOrchestrator.BasePullSpeed);
    }

    [Fact]
    public void ScoutDroneLoadout_OrbitsAndDamagesNearbyHostiles()
    {
        var profile = ProfileAggregate.CreateNew(502);
        profile = UnlockAndEquipDrone(profile);
        Assert.True(profile.DeriveStatistics().HasScoutDrone);
        profile.BeginRun("TX_BEGIN_DRONE");

        var run = new ComposedRunOrchestrator(
            WorldRunIds.CinderBelt,
            profile.Snapshot.ProfileSeed,
            profile.Snapshot.RunIndex,
            profile.ResolveLoadout(),
            profile.DeriveStatistics(),
            recoveryProtocols: false,
            enableThreatDirector: false);

        Assert.False(run.LastScoutDronePresentation.Active);
        run.Step(FlightCommandFrame.Neutral(run.Combat.Tick));
        Assert.True(run.LastScoutDronePresentation.Active);

        var playerPos = run.Combat.Snapshot(run.Combat.Player).Position;
        var enemy = run.Combat.SpawnEnemy(
            new ContentId("ENM_INTERCEPTOR"),
            playerPos + new Vector2(120, 0));
        var start = run.Combat.Snapshot(enemy);
        var startVitality = start.Hull + start.Shield;
        for (var i = 0; i < 120; i++)
            run.Step(FlightCommandFrame.Neutral(run.Combat.Tick));

        Assert.True(run.LastScoutDronePresentation.Active);
        var end = run.Combat.Snapshot(enemy);
        Assert.True(end.Hull + end.Shield < startVitality || end.Destroyed);
    }

    [Fact]
    public void EnemyKill_SpawnsVisibleFerritePickup()
    {
        var profile = ProfileAggregate.CreateNew(503);
        profile.BeginRun("TX_BEGIN_SALVAGE");
        var run = new ComposedRunOrchestrator(
            WorldRunIds.CinderBelt,
            profile.Snapshot.ProfileSeed,
            profile.Snapshot.RunIndex,
            profile.ResolveLoadout(),
            profile.DeriveStatistics(),
            recoveryProtocols: false,
            enableThreatDirector: false);

        var playerPos = run.Combat.Snapshot(run.Combat.Player).Position;
        var enemy = run.Combat.SpawnEnemy(
            new ContentId("ENM_INTERCEPTOR"),
            playerPos + new Vector2(80, 0));
        run.Combat.InflictDamage(enemy, run.Combat.Player, 10_000f, projectile: true);
        run.Step(FlightCommandFrame.Neutral(run.Combat.Tick));

        Assert.Contains(run.Pickups, pickup => pickup.ResourceId == WorldRunIds.Ferrite);
    }

    [Fact]
    public void StationHullAndShieldUpgrades_ApplyOnSpawn()
    {
        var profile = ProfileAggregate.CreateNew(504);
        var snap = profile.Snapshot;
        profile = new ProfileAggregate(snap with
        {
            PurchasedUpgradeIds = ["UPG_REINFORCED_FRAME", "UPG_SHIELD_RESERVOIR"],
            Balances = new ResourceAmounts(500, 20, 20)
        });
        profile.BeginRun("TX_BEGIN_UPG_SPAWN");
        var run = new ComposedRunOrchestrator(
            WorldRunIds.CinderBelt,
            profile.Snapshot.ProfileSeed,
            profile.Snapshot.RunIndex,
            profile.ResolveLoadout(),
            profile.DeriveStatistics(),
            recoveryProtocols: false,
            purchasedUpgradeIds: profile.Snapshot.PurchasedUpgradeIds);

        var player = run.Combat.Snapshot(run.Combat.Player);
        Assert.Equal(125, player.Hull); // 100 + 25
        Assert.Equal(90, player.Shield); // 60 + 30
    }

    private static ProfileAggregate UnlockAndEquipDrone(ProfileAggregate profile)
    {
        var snap = profile.Snapshot;
        profile = new ProfileAggregate(snap with
        {
            PurchasedResearchIds = snap.PurchasedResearchIds
                .Append(ResearchCatalog.HullReinforcement)
                .Append(ResearchCatalog.UtilityDrone)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            RequestedLoadout = snap.RequestedLoadout.With(ModuleSlot.Utility, ModuleCatalog.UtilityDrone)
        });
        return profile;
    }

    private static ComposedRunOrchestrator CreateRun(ulong seed)
    {
        var profile = ProfileAggregate.CreateNew(seed);
        profile.BeginRun($"TX_BEGIN_{seed}");
        return new(
            WorldRunIds.CinderBelt,
            profile.Snapshot.ProfileSeed,
            profile.Snapshot.RunIndex,
            profile.ResolveLoadout(),
            profile.DeriveStatistics(),
            recoveryProtocols: false);
    }

    private static ulong CreateCombatProbe(ulong seed)
    {
        var run = CreateRun(seed);
        run.Combat.SpawnEnemy(new ContentId("ENM_INTERCEPTOR"), new Vector2(80, 0));
        run.Combat.SpawnEnemy(new ContentId("ENM_GUNSHIP"), new Vector2(120, 20));
        run.Combat.SpawnEnemy(new ContentId("ENM_SAPPER"), new Vector2(100, -20));
        for (var i = 0; i < 180; i++)
        {
            run.Queue(new FlightCommandFrame(
                run.Combat.Tick,
                FlightCommandFrame.Quantize(0.2f),
                0,
                FlightCommandFrame.Quantize(1),
                0,
                FlightAction.Fire));
            run.Step();
        }

        return run.Combat.LastStateHash;
    }
}
