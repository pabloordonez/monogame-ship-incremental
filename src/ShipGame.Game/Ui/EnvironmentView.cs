using ShipGame.Domain;
using ShipGame.Persistence;
using ShipGame.Gameplay;
using ShipGame.Telemetry;

namespace ShipGame.Game;

public sealed record EnvironmentView(
    string EnvironmentId,
    bool Accessible,
    string Explanation,
    bool Selected);
