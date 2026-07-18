namespace ShipGame.Domain;

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
