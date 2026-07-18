namespace ShipGame.Domain;

public sealed record MetaProfileSnapshot(
    ulong ProfileSeed,
    long RunIndex,
    ResourceAmounts Balances,
    LifetimeCounters Counters,
    IReadOnlyList<string> PurchasedResearchIds,
    IReadOnlyList<string> PurchasedUpgradeIds,
    IReadOnlyList<string> UnlockedEnvironmentIds,
    LoadoutSelection RequestedLoadout,
    IReadOnlyList<ProfileTransactionReceipt> Transactions,
    GameSettings Settings,
    RunSummarySnapshot? PreviousRun);
