using ShipGame.Domain;

namespace ShipGame.Gameplay;

/// <summary>
/// Maps P3 world-run reward proposals into the P4-owned Domain <see cref="RewardProposal"/>
/// commit DTO without duplicating banking rules.
/// </summary>
public static class RewardHandoff
{
    public static RewardProposal ToProfileProposal(
        WorldRewardProposal world,
        string runId,
        string environmentId,
        LifetimeCounters counterDelta,
        string? transactionId = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID is required.", nameof(runId));
        if (string.IsNullOrWhiteSpace(environmentId))
            throw new ArgumentException("Environment ID is required.", nameof(environmentId));

        var succeeded = world.Outcome == RunOutcome.Success;
        var earned = ToDomain(world.Held);
        var banked = ToDomain(world.Banked);
        var lost = ToDomain(world.Lost);
        var retained = succeeded
            ? ResourceAmounts.Zero
            : banked; // failure retention is already expressed as Banked by P3; Retained mirrors that share for P4 accounting.
        // P4 commit requires Banked+Retained+Lost == Earned.
        // P3 success: Banked=Held, Lost=0. P3 failure: Banked=retention, Lost=remainder.
        // For failure, P4 expects Banked=0 and Retained=failure share. Normalize here.
        if (!succeeded)
        {
            retained = banked;
            banked = ResourceAmounts.Zero;
        }

        return new RewardProposal(
            transactionId ?? $"reward_{world.ProposalId:x16}",
            runId,
            environmentId,
            succeeded,
            earned,
            banked,
            retained,
            lost,
            counterDelta);
    }

    public static ResourceAmounts ToDomain(WorldResourceAmounts amounts) =>
        new(amounts.Ferrite, amounts.Lumen, amounts.DataCores);
}
