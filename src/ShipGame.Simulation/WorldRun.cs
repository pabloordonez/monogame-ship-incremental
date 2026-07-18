using ShipGame.Domain;

namespace ShipGame.Simulation;

public enum RunPhase
{
    Objective,
    Elite,
    Extraction,
    Succeeded,
    Failed
}

public enum RunFactKind
{
    ResourceCellBroken,
    ResourceCollected,
    NormalEnemyDestroyed,
    EliteDestroyed
}

public readonly record struct RunFact(
    ulong FactId,
    RunFactKind Kind,
    ContentId ResourceId = default,
    int Quantity = 0);

public readonly record struct WorldRunTickInput(
    bool Paused = false,
    bool PlayerHullDepleted = false,
    bool PlayerInExtractionZone = false,
    bool InteractHeld = false,
    GridPoint PlayerCell = default,
    bool BehindLargeAsteroid = false,
    int? UpgradeChoiceIndex = null,
    IReadOnlyList<RunFact>? Facts = null);

public enum WorldRunEventKind
{
    HazardWarned,
    HazardDamageRequested,
    ResourceCredited,
    UpgradeThresholdReached,
    UpgradeOffered,
    UpgradeSelected,
    ObjectiveCompleted,
    EliteActivationRequested,
    EliteDefeated,
    DataCoreDropRequested,
    ExtractionActivated,
    ExtractionProgressed,
    ExtractionReset,
    CollapseWarning,
    RunSucceeded,
    RunFailed,
    RewardProposed
}

public readonly record struct WorldRunEvent(
    long Sequence,
    long RunTick,
    WorldRunEventKind Kind,
    ContentId ContentId = default,
    int Amount = 0,
    int SecondaryAmount = 0);

public readonly record struct ResourceAmounts(int Ferrite, int Lumen, int DataCores)
{
    public ResourceAmounts Add(ContentId id, int quantity)
    {
        if (quantity <= 0)
            return this;
        if (id == WorldRunIds.Ferrite)
            return this with { Ferrite = checked(Ferrite + quantity) };
        if (id == WorldRunIds.Lumen)
            return this with { Lumen = checked(Lumen + quantity) };
        if (id == WorldRunIds.DataCore)
            return this with { DataCores = checked(DataCores + quantity) };
        return this;
    }
}

public enum RunOutcome
{
    Success,
    HullFailure,
    DeadlineFailure
}

public sealed record RewardProposal(
    ulong ProposalId,
    RunOutcome Outcome,
    ResourceAmounts Held,
    ResourceAmounts Banked,
    ResourceAmounts Lost);

public readonly record struct ObjectiveProgress(int FerriteCollected, int NormalEnemiesDestroyed)
{
    public bool Complete => FerriteCollected >= 30 && NormalEnemiesDestroyed >= 8;
}

public readonly record struct ThreatState(int NormalEnemyCap, bool MixedArchetypes, bool MaximumThreat);

public sealed class EnvironmentHazardSystem(FieldDescriptor descriptor)
{
    private readonly FieldDescriptor _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    private readonly HashSet<int> _warnings = [];
    private readonly HashSet<int> _resolutions = [];

    public int ShieldRechargeDelayAdditionTicks =>
        _descriptor.Identity.EnvironmentId == WorldRunIds.IonVeil ? 90 : 0;

    public IReadOnlyList<(WorldRunEventKind Kind, HazardDescriptor Hazard)> Resolve(
        long runTick,
        GridPoint playerCell,
        bool behindLargeAsteroid)
    {
        var events = new List<(WorldRunEventKind, HazardDescriptor)>();
        for (var index = 0; index < _descriptor.Hazards.Count; index++)
        {
            var hazard = _descriptor.Hazards[index];
            if (runTick == hazard.WarningTick && _warnings.Add(index))
                events.Add((WorldRunEventKind.HazardWarned, hazard));
            if (runTick != hazard.ResolveTick || !_resolutions.Add(index))
                continue;
            var impacts = _descriptor.Identity.EnvironmentId == WorldRunIds.CinderBelt
                ? !behindLargeAsteroid
                : DistanceSquared(playerCell, hazard.Center) <= (long)hazard.Radius * hazard.Radius;
            if (impacts)
                events.Add((WorldRunEventKind.HazardDamageRequested, hazard));
        }
        return events;
    }

    private static long DistanceSquared(GridPoint left, GridPoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return (long)dx * dx + (long)dy * dy;
    }
}

public sealed class WorldRunSimulation
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
    private readonly HashSet<int> _emittedOffers = [];
    private long _eventSequence;
    private bool _collapseWarningEmitted;
    private bool _eliteDefeated;
    private bool _rewardProposed;

    public WorldRunSimulation(FieldDescriptor descriptor, RandomStreams random, bool recoveryProtocols = false)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(random);
        var validation = EncounterValidator.Validate(descriptor);
        if (!validation.IsValid)
            throw new ArgumentException($"Invalid field descriptor: {string.Join("; ", validation.Issues)}", nameof(descriptor));
        _identity = descriptor.Identity;
        _hazards = new(descriptor);
        _upgrades = new(random);
        _recoveryProtocols = recoveryProtocols;
    }

    public long RunTick { get; private set; }
    public RunPhase Phase { get; private set; } = RunPhase.Objective;
    public ObjectiveProgress Objective { get; private set; }
    public ResourceAmounts HeldResources { get; private set; }
    public int ExtractionProgressTicks { get; private set; }
    public RewardProposal? Reward { get; private set; }
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
        if (_upgrades.PendingOffer is not null)
        {
            EmitOfferIfNeeded(events);
            if (input.UpgradeChoiceIndex is int choice)
            {
                var selected = _upgrades.Choose(choice);
                Emit(events, WorldRunEventKind.UpgradeSelected, selected);
                EmitOfferIfNeeded(events);
            }
            LastEvents = events.AsReadOnly();
            return LastEvents;
        }
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
        if (_eliteDefeated && Phase == RunPhase.Elite)
        {
            Phase = RunPhase.Extraction;
            Emit(events, WorldRunEventKind.EliteDefeated);
            Emit(events, WorldRunEventKind.DataCoreDropRequested, WorldRunIds.DataCore, 1);
            Emit(events, WorldRunEventKind.ExtractionActivated, WorldRunIds.StandardGate);
        }

        EmitOfferIfNeeded(events);
        if (_upgrades.PendingOffer is null)
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
        switch (fact.Kind)
        {
            case RunFactKind.ResourceCellBroken:
                // Charge applies only to resource-bearing cells (catalog Ferrite/Lumen).
                if (fact.ResourceId == WorldRunIds.Ferrite || fact.ResourceId == WorldRunIds.Lumen)
                    AddCharge(3, events);
                break;
            case RunFactKind.NormalEnemyDestroyed:
                Objective = Objective with
                    { NormalEnemiesDestroyed = Math.Min(1_000_000, Objective.NormalEnemiesDestroyed + 1) };
                AddCharge(8, events);
                break;
            case RunFactKind.ResourceCollected:
                if (fact.Quantity <= 0 || fact.Quantity > 1_000_000 ||
                    (fact.ResourceId != WorldRunIds.Ferrite &&
                     fact.ResourceId != WorldRunIds.Lumen &&
                     fact.ResourceId != WorldRunIds.DataCore))
                    break;
                HeldResources = HeldResources.Add(fact.ResourceId, fact.Quantity);
                if (fact.ResourceId == WorldRunIds.Ferrite)
                    Objective = Objective with
                        { FerriteCollected = Math.Min(1_000_000, Objective.FerriteCollected + fact.Quantity) };
                Emit(events, WorldRunEventKind.ResourceCredited, fact.ResourceId, fact.Quantity);
                break;
            case RunFactKind.EliteDestroyed:
                if (Phase == RunPhase.Elite && !_eliteDefeated)
                {
                    _eliteDefeated = true;
                    AddCharge(20, events);
                }
                break;
        }
    }

    private void AddCharge(int amount, List<WorldRunEvent> events)
    {
        foreach (var threshold in _upgrades.AddCharge(amount))
            Emit(events, WorldRunEventKind.UpgradeThresholdReached, amount: threshold);
    }

    private void EmitOfferIfNeeded(List<WorldRunEvent> events)
    {
        if (_upgrades.PendingOffer is not { } offer || !_emittedOffers.Add(offer.Threshold))
            return;
        Emit(events, WorldRunEventKind.UpgradeOffered, amount: offer.Threshold, secondaryAmount: offer.Choices.Count);
    }

    private void AdvanceExtraction(WorldRunTickInput input, List<WorldRunEvent> events)
    {
        if (Phase != RunPhase.Extraction)
            return;
        if (input.PlayerInExtractionZone && input.InteractHeld)
        {
            ExtractionProgressTicks = Math.Min(ExtractionHoldTicks, ExtractionProgressTicks + 1);
            Emit(events, WorldRunEventKind.ExtractionProgressed, amount: ExtractionProgressTicks);
        }
        else if (!input.PlayerInExtractionZone && ExtractionProgressTicks != 0)
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
            : new ResourceAmounts(HeldResources.Ferrite * (_recoveryProtocols ? 50 : 25) / 100, 0, 0);
        var lost = new ResourceAmounts(
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
