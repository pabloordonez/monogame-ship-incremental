using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record MetaProfileDto(
    ulong ProfileSeed,
    long RunIndex,
    ResourceAmountsDto Balances,
    LifetimeCountersDto Counters,
    IReadOnlyList<string> PurchasedResearchIds,
    IReadOnlyList<string>? PurchasedUpgradeIds,
    IReadOnlyList<string> UnlockedEnvironmentIds,
    LoadoutDto RequestedLoadout,
    IReadOnlyList<TransactionReceiptDto> Transactions,
    SettingsDto Settings,
    RunSummaryDto? PreviousRun);
