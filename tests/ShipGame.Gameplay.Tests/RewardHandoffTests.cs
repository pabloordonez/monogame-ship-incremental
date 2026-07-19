using ShipGame.Domain;
using ShipGame.Gameplay;

namespace ShipGame.Gameplay.Tests;

public class RewardHandoffTests
{
    [Fact]
    public void MapsSuccessfulWorldProposalIntoDomainCommitShape()
    {
        var world = new WorldRewardProposal(
            0xabc,
            RunOutcome.Success,
            new WorldResourceAmounts(40, 2, 1),
            new WorldResourceAmounts(40, 2, 1),
            new WorldResourceAmounts(0, 0, 0));

        var proposal = RewardHandoff.ToProfileProposal(
            world,
            runId: "RUN_1",
            environmentId: MetaContentIds.CinderBelt,
            counterDelta: new LifetimeCounters(1, 8, 1, 40, 12, 0));

        var profile = ProfileAggregate.CreateNew(7);
        var result = profile.CommitAcceptedReward(proposal);
        Assert.True(result.Status == ProfileMutationStatus.Applied, result.Code + ": " + result.Message);
        Assert.Equal(new ResourceAmounts(40, 2, 1), profile.Snapshot.Balances);
    }

    [Fact]
    public void MapsFailedWorldProposalWithRetentionIntoDomainCommitShape()
    {
        var world = new WorldRewardProposal(
            0xdef,
            RunOutcome.HullFailure,
            new WorldResourceAmounts(40, 2, 1),
            new WorldResourceAmounts(10, 0, 0),
            new WorldResourceAmounts(30, 2, 1));

        var proposal = RewardHandoff.ToProfileProposal(
            world,
            runId: "RUN_FAIL",
            environmentId: MetaContentIds.CinderBelt,
            counterDelta: new LifetimeCounters(0, 3, 0, 40, 4, 0));

        var profile = ProfileAggregate.CreateNew(9);
        var result = profile.CommitAcceptedReward(proposal);
        Assert.True(result.Status == ProfileMutationStatus.Applied, result.Code + ": " + result.Message);
        Assert.Equal(new ResourceAmounts(10, 0, 0), profile.Snapshot.Balances);
        Assert.Equal(ResourceAmounts.Zero, proposal.Banked);
        Assert.Equal(new ResourceAmounts(10, 0, 0), proposal.Retained);
    }
}
