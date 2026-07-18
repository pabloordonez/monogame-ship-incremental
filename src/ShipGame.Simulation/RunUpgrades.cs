using ShipGame.Domain;

namespace ShipGame.Simulation;

public readonly record struct TemporaryModifiers(
    int WeaponDamageBasisPoints = 10_000,
    int FireRateBasisPoints = 10_000,
    int ShieldCapacityFlat = 0,
    int ShieldRechargeBasisPoints = 10_000,
    int ShieldDelayTicksFlat = 0,
    int HullFlat = 0,
    int SpeedBasisPoints = 10_000,
    int MobilityCooldownBasisPoints = 10_000,
    int MiningDamageBasisPoints = 10_000,
    int PickupRadiusFlat = 0,
    int PullSpeedBasisPoints = 10_000,
    bool ForkedOutput = false,
    bool PenetratingField = false,
    bool ShockTransit = false,
    bool FractureLens = false);

public sealed record UpgradeDefinition(ContentId Id, Func<TemporaryModifiers, TemporaryModifiers> Apply);
public sealed record UpgradeOffer(int Threshold, IReadOnlyList<ContentId> Choices);

public static class RunUpgradeCatalog
{
    public static readonly IReadOnlyList<UpgradeDefinition> All = Array.AsReadOnly(
    [
        Definition("UPG_OVERCHARGED_MUNITIONS", value => value with
            { WeaponDamageBasisPoints = Multiply(value.WeaponDamageBasisPoints, 12_000) }),
        Definition("UPG_RAPID_CYCLING", value => value with
            { FireRateBasisPoints = Multiply(value.FireRateBasisPoints, 11_800) }),
        Definition("UPG_FORKED_OUTPUT", value => value with { ForkedOutput = true }),
        Definition("UPG_PENETRATING_FIELD", value => value with { PenetratingField = true }),
        Definition("UPG_SHIELD_RESERVOIR", value => value with { ShieldCapacityFlat = value.ShieldCapacityFlat + 30 }),
        Definition("UPG_FAST_REBOOT", value => value with
        {
            ShieldDelayTicksFlat = value.ShieldDelayTicksFlat - 60,
            ShieldRechargeBasisPoints = Multiply(value.ShieldRechargeBasisPoints, 12_000)
        }),
        Definition("UPG_REINFORCED_FRAME", value => value with { HullFlat = value.HullFlat + 25 }),
        Definition("UPG_THRUSTER_OVERCLOCK", value => value with
            { SpeedBasisPoints = Multiply(value.SpeedBasisPoints, 11_500) }),
        Definition("UPG_MOBILITY_LOOP", value => value with
            { MobilityCooldownBasisPoints = Multiply(value.MobilityCooldownBasisPoints, 7_000) }),
        Definition("UPG_FRACTURE_LENS", value => value with
        {
            MiningDamageBasisPoints = Multiply(value.MiningDamageBasisPoints, 13_000),
            FractureLens = true
        }),
        Definition("UPG_MAGNETIC_SWEEP", value => value with
        {
            PickupRadiusFlat = value.PickupRadiusFlat + 90,
            PullSpeedBasisPoints = Multiply(value.PullSpeedBasisPoints, 15_000)
        }),
        Definition("UPG_SHOCK_TRANSIT", value => value with { ShockTransit = true })
    ]);

    private static UpgradeDefinition Definition(string id, Func<TemporaryModifiers, TemporaryModifiers> apply) =>
        new(new ContentId(id), apply);

    private static int Multiply(int current, int modifier) =>
        checked((int)((long)current * modifier / 10_000));
}

public sealed class RunUpgradeSystem
{
    public static readonly IReadOnlyList<int> Thresholds = Array.AsReadOnly([30, 75, 135, 210]);
    private readonly Pcg32 _random;
    private readonly Queue<int> _pendingThresholds = new();
    private readonly HashSet<ContentId> _offered = [];
    private readonly HashSet<ContentId> _owned = [];
    private int _nextThreshold;

    public RunUpgradeSystem(RandomStreams random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random.Get(RngStream.Upgrade);
        if (RunUpgradeCatalog.All.Count != 12 ||
            RunUpgradeCatalog.All.Select(definition => definition.Id).Distinct().Count() != 12)
            throw new InvalidOperationException("The MVP upgrade catalog must contain twelve unique IDs.");
    }

    public int Charge { get; private set; }
    public UpgradeOffer? PendingOffer { get; private set; }
    public TemporaryModifiers Modifiers { get; private set; } = new();
    public IReadOnlyCollection<ContentId> Owned => _owned.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
    public bool PausesSimulation => PendingOffer is not null;

    public IReadOnlyList<int> AddCharge(int amount)
    {
        if (amount < 0 || amount > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Charge = Math.Min(1_000_000, Charge + amount);
        var crossed = new List<int>();
        while (_nextThreshold < Thresholds.Count && Charge >= Thresholds[_nextThreshold])
        {
            var threshold = Thresholds[_nextThreshold++];
            _pendingThresholds.Enqueue(threshold);
            crossed.Add(threshold);
        }
        OpenNextOffer();
        return crossed;
    }

    public ContentId Choose(int choiceIndex)
    {
        if (PendingOffer is null)
            throw new InvalidOperationException("No upgrade offer is pending.");
        if ((uint)choiceIndex >= (uint)PendingOffer.Choices.Count)
            throw new ArgumentOutOfRangeException(nameof(choiceIndex));
        var selected = PendingOffer.Choices[choiceIndex];
        if (!_owned.Add(selected))
            throw new InvalidOperationException("Run upgrades cannot repeat.");
        var definition = RunUpgradeCatalog.All.Single(candidate => candidate.Id == selected);
        Modifiers = definition.Apply(Modifiers);
        PendingOffer = null;
        OpenNextOffer();
        return selected;
    }

    public void Clear()
    {
        Charge = 0;
        _nextThreshold = 0;
        _pendingThresholds.Clear();
        _offered.Clear();
        _owned.Clear();
        PendingOffer = null;
        Modifiers = new();
    }

    private void OpenNextOffer()
    {
        if (PendingOffer is not null || !_pendingThresholds.TryDequeue(out var threshold))
            return;
        var eligible = RunUpgradeCatalog.All
            .Select(definition => definition.Id)
            .Where(id => !_offered.Contains(id) && !_owned.Contains(id))
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToList();
        if (eligible.Count < 3)
            throw new InvalidOperationException("The upgrade pool cannot produce three distinct choices.");
        for (var index = eligible.Count - 1; index > 0; index--)
        {
            var swap = EncounterGenerator.NextInt(_random, 0, index + 1);
            (eligible[index], eligible[swap]) = (eligible[swap], eligible[index]);
        }
        var choices = eligible.Take(3).ToArray();
        foreach (var choice in choices)
            _offered.Add(choice);
        PendingOffer = new(threshold, Array.AsReadOnly(choices));
    }
}
