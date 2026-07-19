using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct PendingTemporaryModifier(TemporaryCombatModifiers Value);
