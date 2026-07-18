using ShipGame.Domain;

namespace ShipGame.Simulation;

public readonly record struct ObjectiveProgress(int FerriteCollected, int NormalEnemiesDestroyed)
{
    public bool Complete => FerriteCollected >= 30 && NormalEnemiesDestroyed >= 8;
}
