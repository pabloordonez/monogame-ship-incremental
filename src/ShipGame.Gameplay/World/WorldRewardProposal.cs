using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record WorldRewardProposal(
    ulong ProposalId,
    RunOutcome Outcome,
    WorldResourceAmounts Held,
    WorldResourceAmounts Banked,
    WorldResourceAmounts Lost);
