using ShipGame.Domain;

namespace ShipGame.Simulation;

/// <summary>
/// Legacy charge tracker kept for WorldRun compatibility. Mid-run offers are disabled;
/// station purchases own the UPG catalog.
/// </summary>
public sealed class RunUpgradeSystem
{
    public static readonly IReadOnlyList<int> Thresholds = Array.AsReadOnly([30, 75, 135, 210]);

    public RunUpgradeSystem(RandomStreams random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _ = random.Get(RngStream.Upgrade);
        if (RunUpgradeCatalog.All.Count != 12 ||
            RunUpgradeCatalog.All.Select(definition => definition.Id).Distinct().Count() != 12)
            throw new InvalidOperationException("The MVP upgrade catalog must contain twelve unique IDs.");
        Modifiers = TemporaryModifiers.Identity;
    }

    public int Charge { get; private set; }
    public UpgradeOffer? PendingOffer => null;
    public TemporaryModifiers Modifiers { get; private set; }
    public IReadOnlyCollection<ContentId> Owned { get; private set; } = Array.Empty<ContentId>();
    public bool PausesSimulation => false;

    public IReadOnlyList<int> AddCharge(int amount)
    {
        if (amount < 0 || amount > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Charge = Math.Min(1_000_000, Charge + amount);
        // Mid-run offers removed: charge may accumulate for telemetry but never opens an offer.
        return Array.Empty<int>();
    }

    public ContentId Choose(int choiceIndex) =>
        throw new InvalidOperationException("Mid-run upgrade offers are disabled; purchase upgrades at the Station.");

    public void SeedFromStationPurchases(IEnumerable<string> purchasedUpgradeIds)
    {
        ArgumentNullException.ThrowIfNull(purchasedUpgradeIds);
        var ids = purchasedUpgradeIds
            .Where(id => RunUpgradeCatalog.TryGet(id, out _))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new ContentId(id))
            .ToArray();
        Owned = ids;
        Modifiers = RunUpgradeCatalog.Fold(ids.Select(id => id.Value));
    }

    public void Clear()
    {
        Charge = 0;
        Owned = Array.Empty<ContentId>();
        Modifiers = TemporaryModifiers.Identity;
    }
}
