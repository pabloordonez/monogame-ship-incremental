using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct AiBrain(EnemyBehavior Behavior, int StateTicks, int BurstShotsRemaining, int ActiveMines);
