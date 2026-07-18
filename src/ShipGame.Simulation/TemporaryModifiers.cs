using ShipGame.Domain;

namespace ShipGame.Simulation;

public readonly record struct TemporaryModifiers(
    int WeaponDamageBasisPoints,
    int FireRateBasisPoints,
    int ShieldCapacityFlat,
    int ShieldRechargeBasisPoints,
    int ShieldDelayTicksFlat,
    int HullFlat,
    int SpeedBasisPoints,
    int MobilityCooldownBasisPoints,
    int MiningDamageBasisPoints,
    int PickupRadiusFlat,
    int PullSpeedBasisPoints,
    bool ForkedOutput,
    bool PenetratingField,
    bool ShockTransit,
    bool FractureLens)
{
    /// <summary>1.0× identity. Never use parameterless <c>new()</c> (zeros all fields).</summary>
    public static TemporaryModifiers Identity { get; } = new(
        10_000, 10_000, 0, 10_000, 0, 0, 10_000, 10_000, 10_000, 0, 10_000,
        false, false, false, false);
}
