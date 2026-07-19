using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct ControlIntent(Vector2 Move, Vector2 Aim, FlightAction Actions, FlightAction PreviousActions);
