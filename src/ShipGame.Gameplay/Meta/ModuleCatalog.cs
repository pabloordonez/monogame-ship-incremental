using ShipGame.Domain;

namespace ShipGame.Gameplay;

public static class ModuleCatalog
{
    public const string WeaponPulse = "MOD_WEAPON_PULSE";
    public const string WeaponBeam = "MOD_WEAPON_BEAM";
    public const string WeaponSeeker = "MOD_WEAPON_SEEKER";
    public const string MiningLaser = "MOD_MINING_LASER";
    public const string MiningCharge = "MOD_MINING_CHARGE";
    public const string ShieldCapacitor = "MOD_SHIELD_CAPACITOR";
    public const string ShieldReflective = "MOD_SHIELD_REFLECTIVE";
    public const string EngineVector = "MOD_ENGINE_VECTOR";
    public const string EngineBlink = "MOD_ENGINE_BLINK";
    public const string UtilityTractor = "MOD_UTILITY_TRACTOR";
    public const string UtilityDrone = "MOD_UTILITY_DRONE";

    private static readonly ModuleDefinition[] Definitions =
    [
        new(WeaponPulse, ModuleSlot.Weapon, null, true),
        new(WeaponBeam, ModuleSlot.Weapon, ResearchCatalog.WeaponBeam, false),
        new(WeaponSeeker, ModuleSlot.Weapon, ResearchCatalog.WeaponSeeker, false),
        new(MiningLaser, ModuleSlot.Mining, null, true),
        new(MiningCharge, ModuleSlot.Mining, ResearchCatalog.MiningSeismic, false),
        new(ShieldCapacitor, ModuleSlot.Shield, null, true),
        new(ShieldReflective, ModuleSlot.Shield, ResearchCatalog.ShieldReflective, false),
        new(EngineVector, ModuleSlot.Engine, null, true),
        new(EngineBlink, ModuleSlot.Engine, ResearchCatalog.EngineBlink, false),
        new(UtilityTractor, ModuleSlot.Utility, null, true),
        new(UtilityDrone, ModuleSlot.Utility, ResearchCatalog.UtilityDrone, false)
    ];

    private static readonly IReadOnlyDictionary<string, ModuleDefinition> ById =
        Definitions.ToDictionary(module => module.Id, StringComparer.Ordinal);

    public static IReadOnlyList<ModuleDefinition> All => Definitions;

    public static LoadoutSelection Defaults { get; } = new(
        WeaponPulse,
        MiningLaser,
        ShieldCapacitor,
        EngineVector,
        UtilityTractor);

    public static bool TryGet(string id, out ModuleDefinition definition) =>
        ById.TryGetValue(id, out definition!);

    public static string DefaultFor(ModuleSlot slot) =>
        Definitions.Single(module => module.Slot == slot && module.IsDefault).Id;
}
