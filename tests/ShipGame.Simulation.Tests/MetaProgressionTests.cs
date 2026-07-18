using ShipGame.Domain;

namespace ShipGame.Simulation.Tests;

public class MetaProgressionTests
{
    [Fact]
    public void AcceptedRewardCommitIsAtomicBalancedAndIdempotent()
    {
        var profile = ProfileAggregate.CreateNew(42);
        var proposal = SuccessfulReward("TX_REWARD_1", "RUN_1");

        var applied = profile.CommitAcceptedReward(proposal);
        var afterApplied = profile.Snapshot;
        var duplicate = profile.CommitAcceptedReward(proposal);

        Assert.Equal(ProfileMutationStatus.Applied, applied.Status);
        Assert.Equal(ProfileMutationStatus.Duplicate, duplicate.Status);
        Assert.Equal(new ResourceAmounts(40, 2, 1), afterApplied.Balances);
        Assert.Equal(afterApplied.Balances, profile.Snapshot.Balances);
        Assert.Equal(2, profile.Snapshot.Transactions.Count);

        var invalid = proposal with
        {
            TransactionId = "TX_REWARD_BAD",
            RunId = "RUN_2",
            Lost = new ResourceAmounts(1, 0, 0)
        };
        var beforeInvalid = profile.Snapshot;
        Assert.Equal(ProfileMutationStatus.Rejected, profile.CommitAcceptedReward(invalid).Status);
        Assert.Equal(beforeInvalid.Balances, profile.Snapshot.Balances);
        Assert.Equal(beforeInvalid.Counters, profile.Snapshot.Counters);
        Assert.Equal(beforeInvalid.Transactions.Count, profile.Snapshot.Transactions.Count);
    }

    [Fact]
    public void DifferentTransactionCannotCommitSameRunTwice()
    {
        var profile = ProfileAggregate.CreateNew(42);
        Assert.Equal(
            ProfileMutationStatus.Applied,
            profile.CommitAcceptedReward(SuccessfulReward("TX_REWARD_1", "RUN_1")).Status);

        var second = profile.CommitAcceptedReward(SuccessfulReward("TX_REWARD_2", "RUN_1"));

        Assert.Equal(ProfileMutationStatus.Rejected, second.Status);
        Assert.Equal("reward.run-already-committed", second.Code);
        Assert.Equal(new ResourceAmounts(40, 2, 1), profile.Snapshot.Balances);
    }

    [Fact]
    public void ConflictingTransactionIdCannotMutateProfile()
    {
        var profile = RichProfile();
        Assert.Equal(
            ProfileMutationStatus.Applied,
            profile.PurchaseResearch("TX_SHARED", ResearchCatalog.HullReinforcement).Status);
        var balances = profile.Snapshot.Balances;

        var conflict = profile.EquipModule("TX_SHARED", ModuleSlot.Weapon, ModuleCatalog.WeaponPulse);

        Assert.Equal(ProfileMutationStatus.Rejected, conflict.Status);
        Assert.Equal("transaction.conflict", conflict.Code);
        Assert.Equal(balances, profile.Snapshot.Balances);
    }

    [Fact]
    public void ResearchCatalogHasExactReachableAcyclicTwelveNodeGraph()
    {
        Assert.Empty(ResearchCatalog.ValidateGraph());
        Assert.Equal(12, ResearchCatalog.All.Count);
        Assert.Equal(
            new ResourceAmounts(565, 14, 15),
            ResearchCatalog.All.Aggregate(
                ResourceAmounts.Zero,
                (total, node) => new(
                    total.Ferrite + node.Cost.Ferrite,
                    total.Lumen + node.Cost.Lumen,
                    total.DataCores + node.Cost.DataCores)));

        var profile = RichProfile();
        var purchased = new HashSet<string>(StringComparer.Ordinal);
        var transaction = 0;
        while (purchased.Count < ResearchCatalog.All.Count)
        {
            var ready = ResearchCatalog.All.First(node =>
                !purchased.Contains(node.Id) &&
                node.Dependencies.All(purchased.Contains));
            var result = profile.PurchaseResearch($"TX_RESEARCH_{transaction++}", ready.Id);
            Assert.Equal(ProfileMutationStatus.Applied, result.Status);
            purchased.Add(ready.Id);
        }

        Assert.Equal(12, profile.Snapshot.PurchasedResearchIds.Count);
        Assert.Equal(new ResourceAmounts(435, 986, 985), profile.Snapshot.Balances);
    }

    [Fact]
    public void ResearchPurchaseRejectsCostPrerequisiteAndGateWithoutPartialMutation()
    {
        var profile = ProfileAggregate.CreateNew(9);
        var before = profile.Snapshot;

        Assert.Equal(
            "research.prerequisite",
            profile.PurchaseResearch("TX_PREREQ", ResearchCatalog.ShieldReflective).Code);
        Assert.Equal(
            "research.cost",
            profile.PurchaseResearch("TX_COST", ResearchCatalog.HullReinforcement).Code);

        Assert.Equal(before.Balances, profile.Snapshot.Balances);
        Assert.Empty(profile.Snapshot.PurchasedResearchIds);
        Assert.Empty(profile.Snapshot.Transactions);
    }

    [Fact]
    public void IonVeilAccessQueriesSemanticCapability()
    {
        var withoutCapability = RichProfile();
        Assert.Equal(
            "travel.capability-required",
            withoutCapability.ValidateDestination(MetaContentIds.IonVeil).Code);

        PurchaseIonPath(withoutCapability);

        Assert.True(withoutCapability.HasCapability(MetaContentIds.TravelIonVeil));
        Assert.Equal(
            ProfileMutationStatus.Applied,
            withoutCapability.ValidateDestination(MetaContentIds.IonVeil).Status);
        Assert.False(withoutCapability.HasCapability(ResearchCatalog.NavIonVeil));
    }

    [Fact]
    public void FiveSlotLoadoutFallsBackVisiblyAndPreservesRequestedIds()
    {
        var snapshot = ProfileAggregate.CreateNew(1).Snapshot with
        {
            RequestedLoadout = ModuleCatalog.Defaults with
            {
                Weapon = "MOD_UNKNOWN_RECOVERABLE",
                Engine = ModuleCatalog.WeaponPulse
            }
        };
        var profile = new ProfileAggregate(snapshot);

        var resolved = profile.ResolveLoadout();

        Assert.Equal(ModuleCatalog.WeaponPulse, resolved.Effective.Weapon);
        Assert.Equal(ModuleCatalog.EngineVector, resolved.Effective.Engine);
        Assert.Equal(2, resolved.Diagnostics.Count);
        Assert.Equal("MOD_UNKNOWN_RECOVERABLE", profile.Snapshot.RequestedLoadout.Weapon);
        Assert.Equal(ModuleCatalog.WeaponPulse, profile.Snapshot.RequestedLoadout.Engine);
    }

    [Fact]
    public void DerivedStatisticsAreDeterministicAndPreviewUsesDomainRules()
    {
        var profile = RichProfile();
        PurchaseIonPath(profile);
        Assert.Equal(
            ProfileMutationStatus.Applied,
            profile.EquipModule("TX_EQUIP_SHIELD", ModuleSlot.Shield, ModuleCatalog.ShieldReflective).Status);
        Assert.Equal(
            ProfileMutationStatus.Applied,
            profile.EquipModule("TX_EQUIP_ENGINE", ModuleSlot.Engine, ModuleCatalog.EngineBlink).Status);
        Assert.Equal(
            ProfileMutationStatus.Applied,
            profile.PurchaseResearch("TX_UTILITY_DRONE", ResearchCatalog.UtilityDrone).Status);

        var first = profile.DeriveStatistics();
        var second = profile.DeriveStatistics();
        var preview = profile.InspectModule(ModuleSlot.Utility, ModuleCatalog.UtilityDrone);

        Assert.Equal(first, second);
        Assert.Equal(115, first.MaximumHull);
        Assert.Equal(216, first.MaximumSpeed);
        Assert.Equal(45, first.ShieldCapacity);
        Assert.True(first.HasBlink);
        Assert.True(first.HasReflectiveShield);
        Assert.True(preview.Unlocked);
        Assert.True(preview.Proposed!.HasScoutDrone);
    }

    [Fact]
    public void FailedRewardRetainsFerriteWithoutBanking()
    {
        var profile = ProfileAggregate.CreateNew(5);
        var proposal = new RewardProposal(
            "TX_FAIL",
            "RUN_FAIL",
            MetaContentIds.CinderBelt,
            false,
            new(40, 2, 1),
            ResourceAmounts.Zero,
            new(10, 0, 0),
            new(30, 2, 1),
            new(0, 8, 0, 40, 12, 0));

        Assert.Equal(ProfileMutationStatus.Applied, profile.CommitAcceptedReward(proposal).Status);
        Assert.Equal(new ResourceAmounts(10, 0, 0), profile.Snapshot.Balances);
        Assert.Equal(0, profile.Snapshot.Counters.Extractions);
    }

    [Fact]
    public void InvalidOrOverflowingUntrustedProfileIsRejected()
    {
        var invalid = ProfileAggregate.CreateNew(1).Snapshot with
        {
            Balances = new ResourceAmounts(-1, 0, 0)
        };
        Assert.Throws<ArgumentException>(() => new ProfileAggregate(invalid));

        var overflow = ProfileAggregate.CreateNew(1).Snapshot with
        {
            Balances = new ResourceAmounts(long.MaxValue, 0, 0)
        };
        var profile = new ProfileAggregate(overflow);
        var result = profile.CommitAcceptedReward(
            SuccessfulReward("TX_OVERFLOW", "RUN_OVERFLOW") with
            {
                Earned = new ResourceAmounts(1, 0, 0),
                Banked = new ResourceAmounts(1, 0, 0)
            });
        Assert.Equal("reward.overflow", result.Code);
        Assert.Equal(long.MaxValue, profile.Snapshot.Balances.Ferrite);
    }

    private static ProfileAggregate RichProfile()
    {
        var snapshot = ProfileAggregate.CreateNew(123).Snapshot with
        {
            Balances = new ResourceAmounts(1000, 1000, 1000),
            Counters = new LifetimeCounters(10, 100, 10, 1000, 100, 2)
        };
        return new(snapshot);
    }

    private static void PurchaseIonPath(ProfileAggregate profile)
    {
        foreach (var id in new[]
                 {
                     ResearchCatalog.HullReinforcement,
                     ResearchCatalog.ShieldReflective,
                     ResearchCatalog.EngineTuning,
                     ResearchCatalog.EngineBlink,
                     ResearchCatalog.NavIonVeil
                 })
            Assert.Equal(
                ProfileMutationStatus.Applied,
                profile.PurchaseResearch("TX_" + id, id).Status);
    }

    private static RewardProposal SuccessfulReward(string transactionId, string runId) =>
        new(
            transactionId,
            runId,
            MetaContentIds.CinderBelt,
            true,
            new(40, 2, 1),
            new(40, 2, 1),
            ResourceAmounts.Zero,
            ResourceAmounts.Zero,
            new(1, 8, 1, 40, 12, 0));
}
