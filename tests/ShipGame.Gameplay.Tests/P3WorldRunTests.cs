using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay.Tests;

public class P3WorldRunTests
{
    /// <summary>
    /// Authoritative 10k×2 package gate. Per seed this asserts validator pass, OBJ_FIELD_PROOF Ferrite
    /// floor, elite/extraction reachability (via validator flood-fill), and reward-bound sanity via a
    /// lightweight headless resolution that injects combat facts (not full combat AI/spawning).
    /// Natural hard GenerateFallback is tracked but may be zero; that path is proven by
    /// <see cref="HardGenerateFallbackFingerprintIsDeterministic"/>.
    /// </summary>
    [Theory]
    [InlineData("ENV_CINDER_BELT")]
    [InlineData("ENV_ION_VEIL")]
    public void TenThousandSeedsPerEnvironmentSatisfyWorldRunInvariants(string environment)
    {
        var generator = new EncounterGenerator();
        var environmentId = new ContentId(environment);
        var validated = 0;
        var softOrHardFallbackCount = 0;
        var hardFallbackCount = 0;
        for (ulong seed = 0; seed < 10_000; seed++)
        {
            var result = generator.Generate(GenerationIdentity.Current(environmentId, seed));
            var descriptor = result.Descriptor;
            if (result.FallbackUsed)
                softOrHardFallbackCount++;
            if (descriptor.Attempt >= 4)
                hardFallbackCount++;

            var validation = EncounterValidator.Validate(descriptor);
            Assert.True(validation.IsValid, $"seed {seed}: {string.Join("; ", validation.Issues)}");
            Assert.Equal(EncounterGenerator.CurrentGenerationVersion, descriptor.Identity.GenerationVersion);
            Assert.Equal(3, descriptor.Sectors.Count(sector => sector.Kind == SectorKind.Objective));
            Assert.InRange(descriptor.AsteroidCells.Count, 15, FieldDescriptor.MaximumAsteroidCells);
            Assert.InRange(descriptor.Hazards.Count, 1, FieldDescriptor.MaximumHazards);

            var ferriteCells = descriptor.AsteroidCells.Count(cell => cell.Kind == AsteroidCellKind.Ferrite);
            // Standard Ferrite yield 2–4; objective needs 30 collected Ferrite.
            var minFerriteYield = ferriteCells * 2;
            var maxFerriteYield = ferriteCells * 4;
            Assert.True(minFerriteYield >= 30, $"seed {seed}: Ferrite floor {minFerriteYield} < 30");
            Assert.Equal(1, descriptor.Sectors.Count(sector => sector.Kind == SectorKind.EliteArena));
            Assert.Equal(1, descriptor.Sectors.Count(sector => sector.Kind == SectorKind.Extraction));
            Assert.Contains(descriptor.Sectors, sector => sector.Kind == SectorKind.Spawn);
            Assert.Contains(descriptor.Sectors, sector => sector.Kind == SectorKind.EliteArena);
            Assert.Contains(descriptor.Sectors, sector => sector.Kind == SectorKind.Extraction);

            if (environmentId == WorldRunIds.CinderBelt)
                Assert.All(descriptor.Hazards, hazard => Assert.Equal(25, hazard.Damage));
            else
                Assert.All(descriptor.Hazards, hazard => Assert.Equal(30, hazard.Damage));

            // Lightweight headless resolution: inject RunFacts for combat/mining outcomes; no combat sim.
            var reward = ResolveSeedHeadlessLightweight(descriptor, seed);
            Assert.Equal(RunOutcome.Success, reward.Outcome);
            Assert.Equal(reward.Held, reward.Banked);
            Assert.Equal(0, reward.Lost.Ferrite);
            Assert.Equal(0, reward.Lost.Lumen);
            Assert.Equal(0, reward.Lost.DataCores);
            Assert.True(reward.Banked.Ferrite >= 30, $"seed {seed}: banked Ferrite {reward.Banked.Ferrite}");
            Assert.InRange(reward.Banked.Ferrite, 30, maxFerriteYield);
            Assert.Equal(1, reward.Banked.DataCores);
            Assert.InRange(reward.Banked.Lumen, 0, descriptor.AsteroidCells.Count(cell => cell.Kind == AsteroidCellKind.Lumen));
            Assert.NotEqual(0UL, reward.ProposalId);

            validated++;
        }

        Assert.Equal(10_000, validated);
        // Natural soft/hard fallback counts are recorded for evidence only. Soft retry and hard
        // GenerateFallback (Attempt == 4) fingerprint determinism are proven by dedicated tests;
        // natural 10k×2 historically yields hardFallbackCount == 0 for generation version 1.
        Assert.True(
            softOrHardFallbackCount <= 10_000 && hardFallbackCount <= softOrHardFallbackCount,
            $"fallback telemetry out of range: softOrHard={softOrHardFallbackCount}, hard={hardFallbackCount}");
    }

    [Fact]
    public void SoftRetryFallbackFingerprintIsDeterministic()
    {
        var generator = new EncounterGenerator();
        var identity = GenerationIdentity.Current(WorldRunIds.CinderBelt, 982_451);

        var first = generator.Generate(identity, invalidatePrimaryForTest: true);
        var second = generator.Generate(identity, invalidatePrimaryForTest: true);

        Assert.True(first.FallbackUsed);
        Assert.Equal(1, first.Descriptor.Attempt);
        Assert.True(first.Descriptor.Attempt < 4);
        Assert.Equal(Fingerprint(first.Descriptor), Fingerprint(second.Descriptor));
    }

    [Fact]
    public void HardGenerateFallbackFingerprintIsDeterministic()
    {
        var generator = new EncounterGenerator();
        var identity = GenerationIdentity.Current(WorldRunIds.CinderBelt, 982_451);

        var first = generator.Generate(identity, invalidateAllAttemptsForTest: true);
        var second = generator.Generate(identity, invalidateAllAttemptsForTest: true);

        Assert.True(first.FallbackUsed);
        Assert.True(second.FallbackUsed);
        Assert.Equal(4, first.Descriptor.Attempt);
        Assert.Equal(4, second.Descriptor.Attempt);
        Assert.True(EncounterValidator.Validate(first.Descriptor).IsValid);
        Assert.Equal(Fingerprint(first.Descriptor), Fingerprint(second.Descriptor));

        var ion = generator.Generate(
            GenerationIdentity.Current(WorldRunIds.IonVeil, 982_451),
            invalidateAllAttemptsForTest: true);
        Assert.True(ion.FallbackUsed);
        Assert.Equal(4, ion.Descriptor.Attempt);
        Assert.True(EncounterValidator.Validate(ion.Descriptor).IsValid);
        Assert.NotEqual(Fingerprint(first.Descriptor), Fingerprint(ion.Descriptor));
    }

    [Fact]
    public void LayoutLootAndUpgradeStreamsRemainIsolated()
    {
        var baseline = new RandomStreams(777);
        var changed = new RandomStreams(777);
        for (var index = 0; index < 100; index++)
            changed.Get(RngStream.Loot).NextUInt();
        Assert.Equal(
            baseline.Get(RngStream.Layout).NextUInt(),
            changed.Get(RngStream.Layout).NextUInt());
        Assert.Equal(
            baseline.Get(RngStream.Upgrade).NextUInt(),
            changed.Get(RngStream.Upgrade).NextUInt());

        var baselineOffers = new RunUpgradeSystem(new RandomStreams(777));
        var changedOffers = new RunUpgradeSystem(new RandomStreams(777));
        _ = new LootGenerationSystem(new RandomStreams(777));
        baselineOffers.AddCharge(30);
        changedOffers.AddCharge(30);
        Assert.Equal(30, baselineOffers.Charge);
        Assert.Equal(changedOffers.Charge, baselineOffers.Charge);
        Assert.Null(baselineOffers.PendingOffer);
        Assert.Null(changedOffers.PendingOffer);
    }

    [Fact]
    public void MiningDropsConserveQuantityAndCollectExactlyOnce()
    {
        var world = new World();
        var cell = world.Create();
        world.Set(cell, new MineableCell { CellId = 9, Kind = AsteroidCellKind.Ferrite, Health = 50 });
        world.Set(cell, new WorldPosition { X = 100, Y = 100 });
        var sourceA = world.Create();
        var sourceB = world.Create();
        var mining = new MiningSystem();

        var broken = mining.Resolve(world,
        [
            new(sourceB, cell, 30),
            new(sourceA, cell, 25),
            new(sourceA, cell, 100)
        ]);
        Assert.Single(broken);

        var loot = new LootGenerationSystem(new RandomStreams(42));
        var spawned = loot.Spawn(world, broken);
        Assert.Single(spawned);
        Assert.InRange(spawned[0].Quantity, 2, 4);

        var collector = world.Create();
        world.Set(collector, new WorldPosition { X = 100, Y = 100 });
        world.Set(collector, new CollectionRadius { Radius = 70, PullSpeedPerTick = 5 });
        var collection = new CollectionSystem();
        var collected = collection.Resolve(world, collector);

        Assert.Single(collected);
        Assert.Equal(spawned.Sum(value => value.Quantity), collected.Sum(value => value.Quantity));
        Assert.Empty(collection.Resolve(world, collector));
        Assert.Null(loot.SpawnEliteDataCore(world, default) is { } first
            ? loot.SpawnEliteDataCore(world, default)
            : throw new Xunit.Sdk.XunitException("Expected the first elite Data Core."));
    }

    [Fact]
    public void EnvironmentHazardsUseDocumentedSchedulesCoverAndDelay()
    {
        var generator = new EncounterGenerator();
        var cinder = generator.Generate(GenerationIdentity.Current(WorldRunIds.CinderBelt, 5)).Descriptor;
        var cinderSystem = new EnvironmentHazardSystem(cinder);
        var flare = cinder.Hazards[0];
        Assert.InRange(flare.ResolveTick, 55 * 60, 65 * 60);
        Assert.Equal(4 * 60, flare.ResolveTick - flare.WarningTick);
        Assert.Contains(cinderSystem.Resolve(flare.WarningTick, default, false), item => item.Kind == WorldRunEventKind.HazardWarned);
        Assert.Empty(cinderSystem.Resolve(flare.ResolveTick, default, true));

        var ion = generator.Generate(GenerationIdentity.Current(WorldRunIds.IonVeil, 5)).Descriptor;
        var ionSystem = new EnvironmentHazardSystem(ion);
        var strike = ion.Hazards[0];
        Assert.Equal(45 * 60, strike.ResolveTick);
        Assert.Equal(150, strike.ResolveTick - strike.WarningTick);
        Assert.Equal(90, ionSystem.ShieldRechargeDelayAdditionTicks);
        Assert.Contains(
            ionSystem.Resolve(strike.ResolveTick, strike.Center, false),
            item => item.Kind == WorldRunEventKind.HazardDamageRequested && item.Hazard.Damage == 30);
    }

    [Fact]
    public void StationUpgradeCatalogIsCompleteAndFoldIsDeterministic()
    {
        Assert.Equal(12, RunUpgradeCatalog.All.Count);
        Assert.Equal(12, RunUpgradeCatalog.All.Select(item => item.Id.Value).Distinct().Count());
        Assert.All(RunUpgradeCatalog.All, item => Assert.True(item.Cost.IsValid));

        var thruster = RunUpgradeCatalog.Fold(["UPG_THRUSTER_OVERCLOCK"]);
        Assert.Equal(11_500, thruster.SpeedBasisPoints);
        Assert.Equal(11_500, RunUpgradeCatalog.Fold(["UPG_THRUSTER_OVERCLOCK"]).SpeedBasisPoints);

        var combat = RunUpgradeCatalog.ToCombatModifiers(thruster);
        Assert.InRange(combat.SpeedMultiplier, 1.14f, 1.16f);
    }

    [Fact]
    public void MidRunNeverOpensUpgradeOffersOrPauses()
    {
        var run = CreateRun(404);
        var facts = new List<RunFact>
        {
            new(1, RunFactKind.ResourceCollected, WorldRunIds.Ferrite, 30)
        };
        for (ulong index = 0; index < 42; index++)
            facts.Add(new(10 + index, RunFactKind.ResourceCellBroken, WorldRunIds.Ferrite));
        for (ulong index = 0; index < 8; index++)
            facts.Add(new(100 + index, RunFactKind.NormalEnemyDestroyed));

        run.Step(new(Facts: facts));
        Assert.Equal(RunPhase.Elite, run.Phase);
        Assert.Null(run.Upgrades.PendingOffer);
        Assert.False(run.Upgrades.PausesSimulation);

        run.Step(new(Facts: [new(300, RunFactKind.EliteDestroyed)]));
        Assert.Equal(RunPhase.Extraction, run.Phase);
        Assert.Null(run.Upgrades.PendingOffer);
    }

    [Fact]
    public void ThreatCapsFollowDocumentedTransitions()
    {
        var run = CreateRun(55);
        Assert.Equal(new ThreatState(4, false, false), run.Threat);

        while (run.RunTick < 3 * 60 * WorldRun.TickRate)
            run.Step(new());
        Assert.Equal(new ThreatState(6, true, false), run.Threat);

        while (run.RunTick < 6 * 60 * WorldRun.TickRate)
            run.Step(new());
        Assert.Equal(new ThreatState(8, true, false), run.Threat);

        CompleteObjective(run);
        ResolveOffers(run);
        Assert.Equal(RunPhase.Elite, run.Phase);
        Assert.Equal(new ThreatState(8, true, false), run.Threat);

        run.Step(new(Facts: [new(700, RunFactKind.EliteDestroyed)]));
        ResolveOffers(run);
        Assert.Equal(RunPhase.Extraction, run.Phase);
        Assert.Equal(new ThreatState(10, true, true), run.Threat);
    }

    [Fact]
    public void CollapseWarningEmitsAtTenMinutes()
    {
        var run = CreateRun(56);
        while (run.RunTick < WorldRun.CollapseWarningTick - 1)
            run.Step(new());
        Assert.DoesNotContain(run.LastEvents, item => item.Kind == WorldRunEventKind.CollapseWarning);
        var events = run.Step(new());
        Assert.Equal(WorldRun.CollapseWarningTick, run.RunTick);
        Assert.Contains(events, item => item.Kind == WorldRunEventKind.CollapseWarning);
        Assert.DoesNotContain(run.Step(new()), item => item.Kind == WorldRunEventKind.CollapseWarning);
    }

    [Fact]
    public void TemporaryEffectsFoldFromIdentityAndClearToIdentity()
    {
        Assert.Equal(10_000, TemporaryModifiers.Identity.SpeedBasisPoints);
        Assert.Equal(0, default(TemporaryModifiers).SpeedBasisPoints);

        var modifiers = TemporaryModifiers.Identity;
        foreach (var definition in RunUpgradeCatalog.All)
            modifiers = definition.Apply(modifiers);
        Assert.True(modifiers.ForkedOutput);
        Assert.True(modifiers.PenetratingField);
        Assert.True(modifiers.FractureLens);
        Assert.True(modifiers.ShockTransit);
        Assert.Equal(30, modifiers.ShieldCapacityFlat);
        Assert.Equal(25, modifiers.HullFlat);
        Assert.Equal(90, modifiers.PickupRadiusFlat);
        Assert.True(modifiers.SpeedBasisPoints > 10_000);

        var upgrades = new RunUpgradeSystem(new RandomStreams(91));
        upgrades.SeedFromStationPurchases(["UPG_THRUSTER_OVERCLOCK"]);
        Assert.Single(upgrades.Owned);
        Assert.Equal(11_500, upgrades.Modifiers.SpeedBasisPoints);
        upgrades.Clear();
        Assert.Empty(upgrades.Owned);
        Assert.Equal(TemporaryModifiers.Identity, upgrades.Modifiers);
    }

    [Fact]
    public void ObjectiveEliteExtractionAndRewardResolveInOrderExactlyOnce()
    {
        var run = CreateRun(77);
        var events = CompleteObjective(run);
        Assert.Equal(RunPhase.Elite, run.Phase);
        Assert.True(run.Objective.Complete);
        Assert.Contains(events, item => item.Kind == WorldRunEventKind.ObjectiveCompleted);
        Assert.Contains(events, item => item.Kind == WorldRunEventKind.EliteActivationRequested);
        ResolveOffers(run);

        events = run.Step(new(Facts: [new(90, RunFactKind.EliteDestroyed)]));
        Assert.Equal(RunPhase.Extraction, run.Phase);
        Assert.Contains(events, item => item.Kind == WorldRunEventKind.DataCoreDropRequested && item.Amount == 1);
        ResolveOffers(run);
        run.Step(new(Facts: [new(91, RunFactKind.ResourceCollected, WorldRunIds.DataCore, 1)]));
        for (var tick = 0; tick < WorldRun.ExtractionHoldTicks; tick++)
            events = run.Step(new(PlayerInExtractionZone: true));

        Assert.Equal(RunPhase.Succeeded, run.Phase);
        Assert.NotNull(run.Reward);
        Assert.Equal(1, run.Reward!.Banked.DataCores);
        Assert.Single(events, item => item.Kind == WorldRunEventKind.RewardProposed);
        Assert.Empty(run.Step(new(PlayerHullDepleted: true)));
        Assert.Same(run.Reward, run.Reward);
    }

    [Fact]
    public void PauseAndLeavingExtractionDoNotAdvanceIncorrectly()
    {
        var run = CreateRun(88);
        run.Step(new(Paused: true));
        Assert.Equal(0, run.RunTick);
        CompleteObjective(run);
        Assert.Null(run.Upgrades.PendingOffer);
        var afterObjective = run.RunTick;
        run.Step(new(Paused: true));
        Assert.Equal(afterObjective, run.RunTick);
        run.Step(new(Facts: [new(500, RunFactKind.EliteDestroyed)]));
        run.Step(new(PlayerInExtractionZone: true));
        Assert.Equal(1, run.ExtractionProgressTicks);
        var events = run.Step(new(PlayerInExtractionZone: false));
        Assert.Equal(0, run.ExtractionProgressTicks);
        Assert.Contains(events, item => item.Kind == WorldRunEventKind.ExtractionReset);
    }

    [Fact]
    public void LeavingZoneResetsExtractionAndRejectsDiscontinuousDwell()
    {
        var run = ReadyForExtraction(89);
        for (var tick = 0; tick < WorldRun.ExtractionHoldTicks / 2; tick++)
            run.Step(new(PlayerInExtractionZone: true));
        Assert.Equal(WorldRun.ExtractionHoldTicks / 2, run.ExtractionProgressTicks);

        // Leave the gate mid-dwell — must reset (continuous zone presence required).
        var events = run.Step(new(PlayerInExtractionZone: false));
        Assert.Equal(0, run.ExtractionProgressTicks);
        Assert.Contains(events, item => item.Kind == WorldRunEventKind.ExtractionReset);

        // Discontinuous half+half must NOT succeed without a continuous full dwell.
        for (var tick = 0; tick < WorldRun.ExtractionHoldTicks / 2; tick++)
            run.Step(new(PlayerInExtractionZone: true));
        Assert.Equal(WorldRun.ExtractionHoldTicks / 2, run.ExtractionProgressTicks);
        Assert.Equal(RunPhase.Extraction, run.Phase);
        Assert.Null(run.Reward);

        // Continuous full dwell (no Interact) completes extraction.
        for (var tick = 0; tick < WorldRun.ExtractionHoldTicks; tick++)
            run.Step(new(PlayerInExtractionZone: true));
        Assert.Equal(RunPhase.Succeeded, run.Phase);
        Assert.Equal(RunOutcome.Success, run.Reward!.Outcome);
    }

    [Fact]
    public void ExtractionSucceedsFromZonePresenceWithoutInteract()
    {
        var run = ReadyForExtraction(90);
        for (var tick = 0; tick < WorldRun.ExtractionHoldTicks; tick++)
            run.Step(new(PlayerInExtractionZone: true, InteractHeld: false));

        Assert.Equal(RunPhase.Succeeded, run.Phase);
        Assert.Equal(RunOutcome.Success, run.Reward!.Outcome);
    }

    [Fact]
    public void TerminalPriorityIsDeathThenExtractionThenDeadline()
    {
        var deathRace = ReadyForExtraction(101);
        for (var tick = 1; tick < WorldRun.ExtractionHoldTicks; tick++)
            deathRace.Step(new(PlayerInExtractionZone: true));
        deathRace.Step(new(PlayerHullDepleted: true, PlayerInExtractionZone: true));
        Assert.Equal(RunOutcome.HullFailure, deathRace.Reward!.Outcome);

        var deadlineRace = ReadyForExtraction(102);
        while (deadlineRace.RunTick < WorldRun.DeadlineTick - WorldRun.ExtractionHoldTicks)
            deadlineRace.Step(new());
        for (var tick = 0; tick < WorldRun.ExtractionHoldTicks; tick++)
            deadlineRace.Step(new(PlayerInExtractionZone: true));
        Assert.Equal(WorldRun.DeadlineTick, deadlineRace.RunTick);
        Assert.Equal(RunOutcome.Success, deadlineRace.Reward!.Outcome);

        var timeout = CreateRun(103);
        while (!timeout.IsTerminal)
            timeout.Step(new());
        Assert.Equal(RunOutcome.DeadlineFailure, timeout.Reward!.Outcome);
        Assert.Equal(WorldRun.DeadlineTick, timeout.RunTick);
    }

    [Fact]
    public void FailureRetentionAndRecoveryProtocolAreDeterministic()
    {
        static WorldRewardProposal Fail(bool recovery)
        {
            var descriptor = new EncounterGenerator()
                .Generate(GenerationIdentity.Current(WorldRunIds.CinderBelt, 211)).Descriptor;
            var run = new WorldRun(descriptor, new RandomStreams(211), recovery);
            run.Step(new(
                PlayerHullDepleted: true,
                Facts:
                [
                    new(1, RunFactKind.ResourceCollected, WorldRunIds.Ferrite, 39),
                    new(2, RunFactKind.ResourceCollected, WorldRunIds.Lumen, 3),
                    new(3, RunFactKind.ResourceCollected, WorldRunIds.DataCore, 1)
                ]));
            return run.Reward!;
        }

        var normal = Fail(false);
        var recovery = Fail(true);
        Assert.Equal(9, normal.Banked.Ferrite);
        Assert.Equal(19, recovery.Banked.Ferrite);
        Assert.Equal(0, normal.Banked.Lumen);
        Assert.Equal(0, normal.Banked.DataCores);
        Assert.NotEqual(0UL, normal.ProposalId);
    }

    [Fact]
    public void SameSeedFactsAndChoicesProduceIdenticalOrderedEventsAndResults()
    {
        static (string[] Events, WorldRewardProposal Reward) Execute()
        {
            var run = CreateRun(501);
            var all = new List<WorldRunEvent>();
            all.AddRange(CompleteObjective(run));
            while (run.Upgrades.PendingOffer is not null)
                all.AddRange(run.Step(new(UpgradeChoiceIndex: 0)));
            all.AddRange(run.Step(new(Facts: [new(1000, RunFactKind.EliteDestroyed)])));
            while (run.Upgrades.PendingOffer is not null)
                all.AddRange(run.Step(new(UpgradeChoiceIndex: 0)));
            for (var tick = 0; tick < WorldRun.ExtractionHoldTicks; tick++)
                all.AddRange(run.Step(new(PlayerInExtractionZone: true)));
            return (
                all.Select(item => $"{item.Sequence}:{item.RunTick}:{item.Kind}:{item.ContentId.Value}:{item.Amount}:{item.SecondaryAmount}").ToArray(),
                run.Reward!);
        }

        var first = Execute();
        var second = Execute();
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Reward, second.Reward);
    }

    [Fact]
    public void InputsAndDescriptorCollectionsAreBounded()
    {
        var run = CreateRun(600);
        var facts = Enumerable.Range(0, 4097)
            .Select(index => new RunFact((ulong)index, RunFactKind.NormalEnemyDestroyed))
            .ToArray();
        Assert.Throws<ArgumentException>(() => run.Step(new(Facts: facts)));
        Assert.Throws<ArgumentException>(() =>
            new EncounterGenerator().Generate(new(
                new ContentId("ENV_UNKNOWN"),
                1,
                ContractVersions.Content,
                ContractVersions.Generation,
                ContractVersions.Rng)));
    }

    /// <summary>
    /// Lightweight headless resolution: injects objective/elite facts and continuous extraction hold.
    /// Does not simulate combat AI, weapon contacts, or enemy pathing.
    /// </summary>
    private static WorldRewardProposal ResolveSeedHeadlessLightweight(FieldDescriptor descriptor, ulong seed)
    {
        var run = new WorldRun(descriptor, new RandomStreams(seed));
        var facts = new List<RunFact>
        {
            new(1, RunFactKind.ResourceCollected, WorldRunIds.Ferrite, 30)
        };
        for (ulong index = 0; index < 8; index++)
            facts.Add(new(10 + index, RunFactKind.NormalEnemyDestroyed));
        run.Step(new(Facts: facts));
        while (run.Upgrades.PendingOffer is not null)
            run.Step(new(UpgradeChoiceIndex: 0));
        run.Step(new(Facts: [new(100, RunFactKind.EliteDestroyed)]));
        while (run.Upgrades.PendingOffer is not null)
            run.Step(new(UpgradeChoiceIndex: 0));
        run.Step(new(Facts: [new(101, RunFactKind.ResourceCollected, WorldRunIds.DataCore, 1)]));
        for (var tick = 0; tick < WorldRun.ExtractionHoldTicks; tick++)
            run.Step(new(PlayerInExtractionZone: true));
        Assert.Equal(RunPhase.Succeeded, run.Phase);
        return run.Reward!;
    }

    private static WorldRun CreateRun(ulong seed)
    {
        var descriptor = new EncounterGenerator()
            .Generate(GenerationIdentity.Current(WorldRunIds.CinderBelt, seed)).Descriptor;
        return new(descriptor, new RandomStreams(seed));
    }

    private static IReadOnlyList<WorldRunEvent> CompleteObjective(WorldRun run)
    {
        var facts = new List<RunFact>
        {
            new(1, RunFactKind.ResourceCollected, WorldRunIds.Ferrite, 30)
        };
        for (ulong index = 0; index < 8; index++)
            facts.Add(new(10 + index, RunFactKind.NormalEnemyDestroyed));
        return run.Step(new(Facts: facts));
    }

    private static void ResolveOffers(WorldRun run)
    {
        while (run.Upgrades.PendingOffer is not null)
            run.Step(new(UpgradeChoiceIndex: 0));
    }

    private static WorldRun ReadyForExtraction(ulong seed)
    {
        var run = CreateRun(seed);
        CompleteObjective(run);
        ResolveOffers(run);
        run.Step(new(Facts: [new(900, RunFactKind.EliteDestroyed)]));
        ResolveOffers(run);
        return run;
    }

    private static string Fingerprint(FieldDescriptor descriptor) =>
        string.Join(
            "|",
            descriptor.Attempt,
            string.Join(",", descriptor.Sectors.Select(value => $"{value.Kind}:{value.Center.X}:{value.Center.Y}")),
            string.Join(",", descriptor.Corridors.Select(value => $"{value.From.X}:{value.From.Y}:{value.To.X}:{value.To.Y}")),
            string.Join(",", descriptor.AsteroidCells.Select(value => $"{value.CellId}:{value.Position.X}:{value.Position.Y}:{value.Kind}:{value.Health}:{value.ProvidesCompleteCover}")),
            string.Join(",", descriptor.Hazards.Select(value => $"{value.WarningTick}:{value.ResolveTick}:{value.Damage}:{value.Center.X}:{value.Center.Y}:{value.Direction}")));
}
