using ShipGame.Domain;

namespace ShipGame.Gameplay;

public enum WorldRunEventKind
{
    HazardWarned,
    HazardDamageRequested,
    ResourceCredited,
    UpgradeThresholdReached,
    UpgradeOffered,
    UpgradeSelected,
    ObjectiveCompleted,
    EliteActivationRequested,
    EliteDefeated,
    DataCoreDropRequested,
    ExtractionActivated,
    ExtractionProgressed,
    ExtractionReset,
    CollapseWarning,
    RunSucceeded,
    RunFailed,
    RewardProposed
}
