namespace ShipGame.Domain;

public sealed record RunSummarySnapshot(
    string RunId,
    string EnvironmentId,
    bool Succeeded,
    ResourceAmounts Earned,
    ResourceAmounts Banked,
    ResourceAmounts Retained,
    ResourceAmounts Lost);
