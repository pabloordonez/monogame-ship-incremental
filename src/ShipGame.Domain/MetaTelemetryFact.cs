namespace ShipGame.Domain;

public readonly record struct MetaTelemetryFact(
    MetaTelemetryFactKind Kind,
    int SubjectCode = 0,
    long Amount = 0,
    bool Succeeded = true);
