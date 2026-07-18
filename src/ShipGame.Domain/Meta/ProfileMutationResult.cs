namespace ShipGame.Domain;

public sealed record ProfileMutationResult(
    ProfileMutationStatus Status,
    string Code,
    string Message);
