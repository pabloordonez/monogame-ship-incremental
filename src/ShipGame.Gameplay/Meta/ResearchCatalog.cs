using ShipGame.Domain;

namespace ShipGame.Gameplay;

public static class ResearchCatalog
{
    public const string HullReinforcement = "RES_HULL_REINFORCEMENT";
    public const string ShieldReflective = "RES_SHIELD_REFLECTIVE";
    public const string WeaponBeam = "RES_WEAPON_BEAM";
    public const string WeaponSeeker = "RES_WEAPON_SEEKER";
    public const string MiningSeismic = "RES_MINING_SEISMIC";
    public const string MiningAssay = "RES_MINING_ASSAY";
    public const string EngineTuning = "RES_ENGINE_TUNING";
    public const string EngineBlink = "RES_ENGINE_BLINK";
    public const string UtilityDrone = "RES_UTILITY_DRONE";
    public const string TractorCalibration = "RES_TRACTOR_CALIBRATION";
    public const string NavIonVeil = "RES_NAV_ION_VEIL";
    public const string RecoveryProtocols = "RES_RECOVERY_PROTOCOLS";

    private static readonly ResearchDefinition[] Definitions =
    [
        Node(HullReinforcement, "Layered Bulkheads", 25, 0, 0, [], "None", _ => true, "STAT_HULL_15"),
        Node(ShieldReflective, "Reflective Harmonics", 45, 1, 1, [HullReinforcement], "1 extraction", c => c.Extractions >= 1, "MOD_SHIELD_REFLECTIVE"),
        Node(WeaponBeam, "Coherent Emitters", 35, 0, 1, [], "1 extraction", c => c.Extractions >= 1, "MOD_WEAPON_BEAM"),
        // Catalog "20 lifetime kills" = normal + elite (all combat kills).
        Node(WeaponSeeker, "Seeker Telemetry", 60, 2, 2, [WeaponBeam], "20 lifetime kills",
            c => checked(c.NormalKills + c.EliteKills) >= 20, "MOD_WEAPON_SEEKER"),
        Node(MiningSeismic, "Resonance Charges", 30, 0, 1, [], "60 lifetime Ferrite", c => c.FerriteCollected >= 60, "MOD_MINING_CHARGE"),
        Node(MiningAssay, "Spectral Assay", 50, 2, 1, [MiningSeismic], "40 resource cells", c => c.ResourceCellsBroken >= 40, "STAT_FERRITE_YIELD_15"),
        Node(EngineTuning, "Vector Calibration", 25, 0, 0, [], "None", _ => true, "STAT_SPEED_8_PERCENT"),
        Node(EngineBlink, "Folded Transit", 60, 2, 2, [EngineTuning, ShieldReflective], "3 extractions", c => c.Extractions >= 3, "MOD_ENGINE_BLINK"),
        Node(UtilityDrone, "Autonomous Firefly", 40, 0, 1, [HullReinforcement], "1 elite kill", c => c.EliteKills >= 1, "MOD_UTILITY_DRONE"),
        Node(TractorCalibration, "Wideband Recovery", 45, 1, 0, [UtilityDrone], "150 lifetime Ferrite", c => c.FerriteCollected >= 150, "STAT_PICKUP_RADIUS_35"),
        Node(NavIonVeil, "Ion Sheathing", 80, 4, 3, [ShieldReflective, EngineBlink], "5 extractions and 5 elites", c => c.Extractions >= 5 && c.EliteKills >= 5, MetaContentIds.TravelIonVeil),
        Node(RecoveryProtocols, "Recovery Protocols", 70, 2, 3, [WeaponSeeker, MiningAssay], "1 Ion Veil extraction", c => c.IonVeilExtractions >= 1, "STAT_FAILURE_RETENTION_50")
    ];

    private static readonly IReadOnlyDictionary<string, ResearchDefinition> ById =
        Definitions.ToDictionary(node => node.Id, StringComparer.Ordinal);

    public static IReadOnlyList<ResearchDefinition> All => Definitions;

    public static bool TryGet(string id, out ResearchDefinition definition) =>
        ById.TryGetValue(id, out definition!);

    public static IReadOnlyList<string> ValidateGraph()
    {
        var issues = new List<string>();
        if (Definitions.Length != 12)
            issues.Add("The MVP research graph must contain exactly twelve nodes.");
        if (Definitions.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() != Definitions.Length)
            issues.Add("Research IDs must be unique.");
        foreach (var node in Definitions)
        {
            if (!MetaContentIds.IsCanonical(node.Id) || !node.Cost.IsValid)
                issues.Add($"Research node '{node.Id}' is invalid.");
            foreach (var dependency in node.Dependencies)
                if (!ById.ContainsKey(dependency))
                    issues.Add($"Research node '{node.Id}' has missing dependency '{dependency}'.");
        }

        var remaining = Definitions.ToDictionary(
            node => node.Id,
            node => node.Dependencies.Count,
            StringComparer.Ordinal);
        var reachable = new Queue<string>(remaining.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var visited = 0;
        while (reachable.TryDequeue(out var id))
        {
            visited++;
            foreach (var dependent in Definitions.Where(node => node.Dependencies.Contains(id, StringComparer.Ordinal)))
                if (--remaining[dependent.Id] == 0)
                    reachable.Enqueue(dependent.Id);
        }
        if (visited != Definitions.Length)
            issues.Add("Research graph contains a cycle or unreachable dependency chain.");
        return issues;
    }

    private static ResearchDefinition Node(
        string id,
        string name,
        long ferrite,
        long lumen,
        long dataCores,
        IReadOnlyList<string> dependencies,
        string gateDescription,
        Func<LifetimeCounters, bool> gate,
        string grant) =>
        new(id, name, new(ferrite, lumen, dataCores), dependencies, gateDescription, gate, grant);
}
