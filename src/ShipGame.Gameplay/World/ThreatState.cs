using ShipGame.Domain;

namespace ShipGame.Simulation;

public readonly record struct ThreatState(int NormalEnemyCap, bool MixedArchetypes, bool MaximumThreat);
