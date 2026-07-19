using ShipGame.Domain;

namespace ShipGame.Gameplay;

/// <summary>
/// Station-purchasable run modifiers (former mid-run UPG catalog) with banked resource costs.
/// </summary>
public static class RunUpgradeCatalog
{
    public static readonly IReadOnlyList<UpgradeDefinition> All = Array.AsReadOnly(
    [
        Definition("UPG_OVERCHARGED_MUNITIONS", 30, 0, 0, value => value with
            { WeaponDamageBasisPoints = Multiply(value.WeaponDamageBasisPoints, 12_000) }),
        Definition("UPG_RAPID_CYCLING", 30, 0, 0, value => value with
            { FireRateBasisPoints = Multiply(value.FireRateBasisPoints, 11_800) }),
        Definition("UPG_FORKED_OUTPUT", 40, 1, 0, value => value with { ForkedOutput = true }),
        Definition("UPG_PENETRATING_FIELD", 40, 1, 0, value => value with { PenetratingField = true }),
        Definition("UPG_SHIELD_RESERVOIR", 35, 0, 1, value => value with { ShieldCapacityFlat = value.ShieldCapacityFlat + 30 }),
        Definition("UPG_FAST_REBOOT", 45, 1, 1, value => value with
        {
            ShieldDelayTicksFlat = value.ShieldDelayTicksFlat - 60,
            ShieldRechargeBasisPoints = Multiply(value.ShieldRechargeBasisPoints, 12_000)
        }),
        Definition("UPG_REINFORCED_FRAME", 35, 0, 0, value => value with { HullFlat = value.HullFlat + 25 }),
        Definition("UPG_THRUSTER_OVERCLOCK", 30, 0, 0, value => value with
            { SpeedBasisPoints = Multiply(value.SpeedBasisPoints, 11_500) }),
        Definition("UPG_MOBILITY_LOOP", 40, 1, 0, value => value with
            { MobilityCooldownBasisPoints = Multiply(value.MobilityCooldownBasisPoints, 7_000) }),
        Definition("UPG_FRACTURE_LENS", 45, 1, 1, value => value with
        {
            MiningDamageBasisPoints = Multiply(value.MiningDamageBasisPoints, 13_000),
            FractureLens = true
        }),
        Definition("UPG_MAGNETIC_SWEEP", 40, 0, 1, value => value with
        {
            PickupRadiusFlat = value.PickupRadiusFlat + 90,
            PullSpeedBasisPoints = Multiply(value.PullSpeedBasisPoints, 15_000)
        }),
        Definition("UPG_SHOCK_TRANSIT", 50, 2, 1, value => value with { ShockTransit = true })
    ]);

    private static readonly IReadOnlyDictionary<string, UpgradeDefinition> ById =
        All.ToDictionary(definition => definition.Id.Value, StringComparer.Ordinal);

    public static bool TryGet(string id, out UpgradeDefinition definition) =>
        ById.TryGetValue(id, out definition!);

    public static TemporaryModifiers Fold(IEnumerable<string> purchasedUpgradeIds)
    {
        ArgumentNullException.ThrowIfNull(purchasedUpgradeIds);
        var modifiers = TemporaryModifiers.Identity;
        foreach (var id in purchasedUpgradeIds.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!TryGet(id, out var definition))
                continue;
            modifiers = definition.Apply(modifiers);
        }

        return modifiers;
    }

    public static TemporaryCombatModifiers ToCombatModifiers(TemporaryModifiers modifiers) =>
        new(
            DamageMultiplier: Math.Clamp(modifiers.WeaponDamageBasisPoints / 10_000f, 0.1f, 10f),
            FireRateMultiplier: Math.Clamp(modifiers.FireRateBasisPoints / 10_000f, 0.1f, 10f),
            SpeedMultiplier: Math.Clamp(modifiers.SpeedBasisPoints / 10_000f, 0.1f, 10f),
            MobilityCooldownMultiplier: Math.Clamp(modifiers.MobilityCooldownBasisPoints / 10_000f, 0.1f, 10f),
            ExtraProjectiles: modifiers.ForkedOutput ? 1 : 0,
            ExtraProjectileDamageMultiplier: 0.6f,
            PierceCount: modifiers.PenetratingField ? 1 : 0,
            ShockTransit: modifiers.ShockTransit);

    private static UpgradeDefinition Definition(
        string id,
        int ferrite,
        int lumen,
        int cores,
        Func<TemporaryModifiers, TemporaryModifiers> apply) =>
        new(new ContentId(id), new ResourceAmounts(ferrite, lumen, cores), apply);

    private static int Multiply(int current, int modifier) =>
        checked((int)((long)current * modifier / 10_000));
}
