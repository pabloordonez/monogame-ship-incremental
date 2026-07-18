using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed record WorldRewardProposal(
    ulong ProposalId,
    RunOutcome Outcome,
    WorldResourceAmounts Held,
    WorldResourceAmounts Banked,
    WorldResourceAmounts Lost);
