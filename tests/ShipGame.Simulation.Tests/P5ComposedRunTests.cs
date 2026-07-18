using System.Numerics;
using ShipGame.Domain;
using ShipGame.Simulation;

namespace ShipGame.Simulation.Tests;

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
