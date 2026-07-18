using ShipGame.Domain;

namespace ShipGame.Telemetry;

public readonly record struct MetaTelemetryContext(
    ulong InstallId,
    ulong SessionId,
    ulong RunId,
    int BuildCode,
    int ContentCode,
    int GenerationVersion,
    long ElapsedMilliseconds);
