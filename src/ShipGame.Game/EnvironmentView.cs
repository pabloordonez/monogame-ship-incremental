using ShipGame.Domain;
using ShipGame.Persistence;
using ShipGame.Simulation;
using ShipGame.Telemetry;

namespace ShipGame.Game;

public sealed record EnvironmentView(
    string EnvironmentId,
    bool Accessible,
    string Explanation,
    bool Selected);
