using ShipGame.Domain;
using ShipGame.Persistence;
using ShipGame.Gameplay;
using ShipGame.Telemetry;

namespace ShipGame.Game;

public sealed record UiActionResult(bool Accepted, string Code, string Message);
