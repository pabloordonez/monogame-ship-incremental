namespace ShipGame.Domain;

public static class MetaContentIds
{
    public const string Ferrite = "MAT_FERRITE";
    public const string Lumen = "MAT_LUMEN";
    public const string DataCore = "MAT_DATA_CORE";
    public const string CinderBelt = "ENV_CINDER_BELT";
    public const string IonVeil = "ENV_ION_VEIL";
    public const string TravelIonVeil = "CAP_TRAVEL_ION_VEIL";

    public static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_');
}

public readonly record struct ResourceAmounts(long Ferrite, long Lumen, long DataCores)
{
    public static ResourceAmounts Zero => new(0, 0, 0);

    public bool IsValid => Ferrite >= 0 && Lumen >= 0 && DataCores >= 0;

    public static bool TryAdd(ResourceAmounts left, ResourceAmounts right, out ResourceAmounts result)
    {
        try
        {
            result = new(
                checked(left.Ferrite + right.Ferrite),
                checked(left.Lumen + right.Lumen),
                checked(left.DataCores + right.DataCores));
            return result.IsValid;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
    }

    public static bool TrySubtract(ResourceAmounts balance, ResourceAmounts cost, out ResourceAmounts result)
    {
        if (!balance.IsValid || !cost.IsValid ||
            balance.Ferrite < cost.Ferrite ||
            balance.Lumen < cost.Lumen ||
            balance.DataCores < cost.DataCores)
        {
            result = default;
            return false;
        }

        result = new(
            balance.Ferrite - cost.Ferrite,
            balance.Lumen - cost.Lumen,
            balance.DataCores - cost.DataCores);
        return true;
    }
}

public readonly record struct LifetimeCounters(
    long Extractions,
    long NormalKills,
    long EliteKills,
    long FerriteCollected,
    long ResourceCellsBroken,
    long IonVeilExtractions)
{
    public static LifetimeCounters Zero => new(0, 0, 0, 0, 0, 0);

    public bool IsValid =>
        Extractions >= 0 &&
        NormalKills >= 0 &&
        EliteKills >= 0 &&
        FerriteCollected >= 0 &&
        ResourceCellsBroken >= 0 &&
        IonVeilExtractions >= 0;

    public static bool TryAdd(LifetimeCounters left, LifetimeCounters right, out LifetimeCounters result)
    {
        try
        {
            result = new(
                checked(left.Extractions + right.Extractions),
                checked(left.NormalKills + right.NormalKills),
                checked(left.EliteKills + right.EliteKills),
                checked(left.FerriteCollected + right.FerriteCollected),
                checked(left.ResourceCellsBroken + right.ResourceCellsBroken),
                checked(left.IonVeilExtractions + right.IonVeilExtractions));
            return result.IsValid;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
    }
}

public enum ModuleSlot
{
    Weapon,
    Mining,
    Shield,
    Engine,
    Utility
}

public sealed record LoadoutSelection(
    string Weapon,
    string Mining,
    string Shield,
    string Engine,
    string Utility)
{
    public string For(ModuleSlot slot) => slot switch
    {
        ModuleSlot.Weapon => Weapon,
        ModuleSlot.Mining => Mining,
        ModuleSlot.Shield => Shield,
        ModuleSlot.Engine => Engine,
        ModuleSlot.Utility => Utility,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public LoadoutSelection With(ModuleSlot slot, string moduleId) => slot switch
    {
        ModuleSlot.Weapon => this with { Weapon = moduleId },
        ModuleSlot.Mining => this with { Mining = moduleId },
        ModuleSlot.Shield => this with { Shield = moduleId },
        ModuleSlot.Engine => this with { Engine = moduleId },
        ModuleSlot.Utility => this with { Utility = moduleId },
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}

public sealed record GameSettings(
    int MasterVolume,
    int MusicVolume,
    int EffectsVolume,
    bool Vibration,
    bool ScreenShake,
    bool Flashes,
    bool Fullscreen,
    bool TelemetryConsent)
{
    public static GameSettings Default { get; } = new(100, 80, 100, true, true, true, false, false);

    public bool IsValid =>
        MasterVolume is >= 0 and <= 100 &&
        MusicVolume is >= 0 and <= 100 &&
        EffectsVolume is >= 0 and <= 100;
}

public sealed record RunSummarySnapshot(
    string RunId,
    string EnvironmentId,
    bool Succeeded,
    ResourceAmounts Earned,
    ResourceAmounts Banked,
    ResourceAmounts Retained,
    ResourceAmounts Lost);

public sealed record ProfileTransactionReceipt(string TransactionId, string Operation, ulong Fingerprint);

public sealed record MetaProfileSnapshot(
    ulong ProfileSeed,
    long RunIndex,
    ResourceAmounts Balances,
    LifetimeCounters Counters,
    IReadOnlyList<string> PurchasedResearchIds,
    IReadOnlyList<string> UnlockedEnvironmentIds,
    LoadoutSelection RequestedLoadout,
    IReadOnlyList<ProfileTransactionReceipt> Transactions,
    GameSettings Settings,
    RunSummarySnapshot? PreviousRun);

/// <summary>
/// Accepted run-reward proposal consumed by profile commits.
/// P4-owned DTO; supersedes narrow foundation <see cref="ProfileSnapshot"/> for meta balances.
/// </summary>
public sealed record RewardProposal(
    string TransactionId,
    string RunId,
    string EnvironmentId,
    bool Succeeded,
    ResourceAmounts Earned,
    ResourceAmounts Banked,
    ResourceAmounts Retained,
    ResourceAmounts Lost,
    LifetimeCounters CounterDelta);

public enum ProfileMutationStatus
{
    Applied,
    Duplicate,
    Rejected
}

public sealed record ProfileMutationResult(
    ProfileMutationStatus Status,
    string Code,
    string Message);

public enum MetaTelemetryFactKind
{
    ScreenEntered,
    NewProfile,
    ContinueProfile,
    EnvironmentSelected,
    LockInspected,
    RunStarted,
    RunResolved,
    ResearchViewed,
    ResearchPurchased,
    ResearchRejected,
    LoadoutChanged,
    SaveStarted,
    SaveSucceeded,
    SaveFailed,
    SaveRecovered,
    OptionChanged
}

public readonly record struct MetaTelemetryFact(
    MetaTelemetryFactKind Kind,
    int SubjectCode = 0,
    long Amount = 0,
    bool Succeeded = true);
