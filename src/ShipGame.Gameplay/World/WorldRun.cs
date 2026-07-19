using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed class WorldRun
{
    public const int TickRate = 60;
    public const long CollapseWarningTick = 10 * 60 * TickRate;
    public const long DeadlineTick = 12 * 60 * TickRate;
    public const int ExtractionHoldTicks = 6 * TickRate;
    private const int MaximumFactsPerTick = 4096;
    private const int MaximumAcceptedFacts = 100_000;

    private readonly GenerationIdentity _identity;
    private readonly EnvironmentHazardSystem _hazards;
    private readonly RunUpgradeSystem _upgrades;
    private readonly bool _recoveryProtocols;
    private readonly HashSet<ulong> _acceptedFactIds = [];
    private readonly RunFactHandlerRegistry _factHandlers = RunFactHandlerRegistry.Create();
    private long _eventSequence;
    private bool _collapseWarningEmitted;
    private int _elitesDefeated;
    private bool _rewardProposed;
    private readonly int _elitesRequired;

    public WorldRun(FieldDescriptor descriptor, RandomStreams random, bool recoveryProtocols = false)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(random);
        var validation = EncounterValidator.Validate(descriptor);
        if (!validation.IsValid)
            throw new ArgumentException($"Invalid field descriptor: {string.Join("; ", validation.Issues)}", nameof(descriptor));
        _identity = descriptor.Identity;
        _elitesRequired = descriptor.Identity.EnvironmentId == WorldRunIds.IonVeil ? 2 : 1;
        _hazards = new(descriptor);
        _upgrades = new(random);
        _recoveryProtocols = recoveryProtocols;
    }

    /// <summary>Elites that must be defeated before extraction (1 Cinder / 2 Ion Veil).</summary>
    public int ElitesRequired => _elitesRequired;

    public long RunTick { get; private set; }
    public RunPhase Phase { get; private set; } = RunPhase.Objective;
    public ObjectiveProgress Objective { get; private set; }
    public WorldResourceAmounts HeldResources { get; private set; }
    public long ResourceCellsBroken { get; private set; }
    public int ExtractionProgressTicks { get; private set; }
    public WorldRewardProposal? Reward { get; private set; }
    public IReadOnlyList<WorldRunEvent> LastEvents { get; private set; } = Array.Empty<WorldRunEvent>();
    public RunUpgradeSystem Upgrades => _upgrades;
    public int ShieldRechargeDelayAdditionTicks => _hazards.ShieldRechargeDelayAdditionTicks;
    public bool IsTerminal => Phase is RunPhase.Succeeded or RunPhase.Failed;

    public ThreatState Threat
    {
        get
        {
            if (Phase == RunPhase.Extraction)
                return new(10, true, true);
            if (Phase == RunPhase.Elite)
                return new(8, true, false);
            if (RunTick >= 6 * 60 * TickRate)
                return new(8, true, false);
            if (RunTick >= 3 * 60 * TickRate)
                return new(6, true, false);
            return new(4, false, false);
        }
    }

    public IReadOnlyList<WorldRunEvent> Step(WorldRunTickInput input)
    {
        if (IsTerminal)
        {
            LastEvents = Array.Empty<WorldRunEvent>();
            return LastEvents;
        }

        var events = new List<WorldRunEvent>();
        if (input.Paused)
        {
            LastEvents = Array.Empty<WorldRunEvent>();
            return LastEvents;
        }

        RunTick++;
        foreach (var (kind, hazard) in _hazards.Resolve(RunTick, input.PlayerCell, input.BehindLargeAsteroid))
            Emit(events, kind, _identity.EnvironmentId, hazard.Damage, hazard.Direction);

        var facts = (input.Facts ?? Array.Empty<RunFact>()).Take(MaximumFactsPerTick + 1).ToArray();
        if (facts.Length > MaximumFactsPerTick)
            throw new ArgumentException("Run fact limit exceeded.", nameof(input));
        foreach (var fact in facts.OrderBy(fact => fact.FactId))
            ConsumeFact(fact, events);

        if (Phase == RunPhase.Objective && Objective.Complete)
        {
            Phase = RunPhase.Elite;
            Emit(events, WorldRunEventKind.ObjectiveCompleted, WorldRunIds.FieldProof);
            Emit(events, WorldRunEventKind.EliteActivationRequested);
        }
        if (_elitesDefeated >= _elitesRequired && Phase == RunPhase.Elite)
        {
            Phase = RunPhase.Extraction;
            Emit(events, WorldRunEventKind.EliteDefeated);
            Emit(events, WorldRunEventKind.ExtractionActivated, WorldRunIds.StandardGate);
        }

        AdvanceExtraction(input, events);
        if (RunTick >= CollapseWarningTick && !_collapseWarningEmitted)
        {
            _collapseWarningEmitted = true;
            Emit(events, WorldRunEventKind.CollapseWarning);
        }

        var extractionCompleted = Phase == RunPhase.Extraction && ExtractionProgressTicks >= ExtractionHoldTicks;
        var deadlineReached = RunTick >= DeadlineTick;
        if (input.PlayerHullDepleted)
            Resolve(RunOutcome.HullFailure, events);
        else if (extractionCompleted)
            Resolve(RunOutcome.Success, events);
        else if (deadlineReached)
            Resolve(RunOutcome.DeadlineFailure, events);

        LastEvents = events.AsReadOnly();
        return LastEvents;
    }

    private void ConsumeFact(RunFact fact, List<WorldRunEvent> events)
    {
        if (_acceptedFactIds.Count >= MaximumAcceptedFacts)
            throw new InvalidOperationException("Accepted run fact bound exceeded.");
        if (!_acceptedFactIds.Add(fact.FactId))
            return;
        _factHandlers.Dispatch(in fact, this, events);
    }

    internal void ApplyResourceCellBrokenFact() =>
        ResourceCellsBroken = Math.Min(1_000_000_000, ResourceCellsBroken + 1);

    internal void ApplyNormalEnemyDestroyedFact() =>
        Objective = Objective with
            { NormalEnemiesDestroyed = Math.Min(1_000_000, Objective.NormalEnemiesDestroyed + 1) };

    internal void ApplyResourceCollectedFact(in RunFact fact, List<WorldRunEvent> events)
    {
        if (fact.Quantity <= 0 || fact.Quantity > 1_000_000 ||
            (fact.ResourceId != WorldRunIds.Ferrite &&
             fact.ResourceId != WorldRunIds.Lumen &&
             fact.ResourceId != WorldRunIds.DataCore))
            return;
        HeldResources = HeldResources.Add(fact.ResourceId, fact.Quantity);
        if (fact.ResourceId == WorldRunIds.Ferrite)
            Objective = Objective with
                { FerriteCollected = Math.Min(1_000_000, Objective.FerriteCollected + fact.Quantity) };
        Emit(events, WorldRunEventKind.ResourceCredited, fact.ResourceId, fact.Quantity);
    }

    internal void ApplyEliteDestroyedFact(List<WorldRunEvent> events)
    {
        if (Phase != RunPhase.Elite || _elitesDefeated >= _elitesRequired)
            return;
        _elitesDefeated++;
        // One Data Core per elite death (Ion Veil: two elites → two cores).
        Emit(events, WorldRunEventKind.DataCoreDropRequested, WorldRunIds.DataCore, 1);
    }

    private void AdvanceExtraction(WorldRunTickInput input, List<WorldRunEvent> events)
    {
        if (Phase != RunPhase.Extraction)
            return;
        // Continuous dwell in the extract gate zone. Leaving the zone resets.
        if (input.PlayerInExtractionZone)
        {
            ExtractionProgressTicks = Math.Min(ExtractionHoldTicks, ExtractionProgressTicks + 1);
            Emit(events, WorldRunEventKind.ExtractionProgressed, amount: ExtractionProgressTicks);
        }
        else if (ExtractionProgressTicks != 0)
        {
            ExtractionProgressTicks = 0;
            Emit(events, WorldRunEventKind.ExtractionReset);
        }
    }

    private void Resolve(RunOutcome outcome, List<WorldRunEvent> events)
    {
        if (IsTerminal)
            return;
        Phase = outcome == RunOutcome.Success ? RunPhase.Succeeded : RunPhase.Failed;
        Emit(events, outcome == RunOutcome.Success ? WorldRunEventKind.RunSucceeded : WorldRunEventKind.RunFailed);
        if (_rewardProposed)
            return;
        _rewardProposed = true;
        var banked = outcome == RunOutcome.Success
            ? HeldResources
            : new WorldResourceAmounts(HeldResources.Ferrite * (_recoveryProtocols ? 50 : 25) / 100, 0, 0);
        var lost = new WorldResourceAmounts(
            HeldResources.Ferrite - banked.Ferrite,
            HeldResources.Lumen - banked.Lumen,
            HeldResources.DataCores - banked.DataCores);
        Reward = new(CreateProposalId(outcome), outcome, HeldResources, banked, lost);
        Emit(events, WorldRunEventKind.RewardProposed, amount: banked.Ferrite, secondaryAmount: banked.Lumen);
        _upgrades.Clear();
    }

    private ulong CreateProposalId(RunOutcome outcome)
    {
        var hash = StableHash.Add(StableHash.Offset, _identity.RunSeed);
        hash = StableHash.Add(hash, _identity.EnvironmentId.Value);
        hash = StableHash.Add(hash, unchecked((ulong)_identity.GenerationVersion));
        return StableHash.Add(hash, unchecked((ulong)outcome));
    }

    private void Emit(
        List<WorldRunEvent> events,
        WorldRunEventKind kind,
        ContentId contentId = default,
        int amount = 0,
        int secondaryAmount = 0) =>
        events.Add(new(++_eventSequence, RunTick, kind, contentId, amount, secondaryAmount));
}
