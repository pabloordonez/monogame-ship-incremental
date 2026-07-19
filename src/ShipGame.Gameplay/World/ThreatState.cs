using ShipGame.Domain;

namespace ShipGame.Gameplay;

public readonly record struct ThreatState(int NormalEnemyCap, bool MixedArchetypes, bool MaximumThreat);
