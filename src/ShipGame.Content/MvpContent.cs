using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed record AssetManifestV1(int SchemaVersion, int BuildVersion, IReadOnlyList<AssetRecord> Assets);

public sealed record AssetRecord(
    string Id,
    string Kind,
    string Source,
    string Status,
    string Owner,
    string License,
    string Attribution,
    string? SourceUrl,
    string Provenance,
    string ReplacementCriterion,
    string? Waiver,
    int? Width,
    int? Height,
    string? Metadata,
    string? SourceHash);

public sealed record CatalogDocument(
    int SchemaVersion,
    string CatalogVersion,
    IReadOnlyList<CatalogDefinition> Definitions);

public sealed record CatalogDefinition(
    string Id,
    string Kind,
    IReadOnlyList<string>? References,
    IReadOnlyDictionary<string, double>? Values);

public sealed record AtlasDocument(
    int SchemaVersion,
    string TextureAssetId,
    int Width,
    int Height,
    int Padding,
    int Extrusion,
    bool RotatedPacking,
    IReadOnlyList<AtlasRegion> Regions,
    IReadOnlyList<CollisionShape> Collisions);

public sealed record AtlasRegion(
    string Id,
    int X,
    int Y,
    int Width,
    int Height,
    double PivotX,
    double PivotY,
    string? Collision,
    IReadOnlyDictionary<string, AtlasPoint>? Hardpoints,
    AnimationMetadata? Animation);

public sealed record AtlasPoint(int X, int Y);
public sealed record AnimationMetadata(int Fps, IReadOnlyList<int> Frames);
public sealed record CollisionShape(string Id, string Kind, IReadOnlyList<double> Values);

public sealed class RuntimeContentCatalog
{
    private readonly IReadOnlyDictionary<string, CatalogDefinition> _definitions;
    private readonly IReadOnlyDictionary<string, AssetRecord> _assets;
    private readonly IReadOnlyDictionary<string, AtlasRegion> _regions;

    internal RuntimeContentCatalog(
        CatalogDocument document,
        IEnumerable<AssetRecord> assets,
        IEnumerable<AtlasRegion> regions,
        string fingerprint)
    {
        CatalogVersion = document.CatalogVersion;
        Fingerprint = fingerprint;
        _definitions = document.Definitions.ToDictionary(item => item.Id, StringComparer.Ordinal);
        _assets = assets.ToDictionary(item => item.Id, StringComparer.Ordinal);
        _regions = regions.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    public string CatalogVersion { get; }
    public string Fingerprint { get; }
    public IEnumerable<string> DefinitionIds => _definitions.Keys.Order(StringComparer.Ordinal);
    public IEnumerable<string> AssetIds => _assets.Keys.Order(StringComparer.Ordinal);
    public IEnumerable<string> RegionIds => _regions.Keys.Order(StringComparer.Ordinal);
    public CatalogDefinition GetDefinition(string id) => _definitions.TryGetValue(id, out var value)
        ? value
        : throw new KeyNotFoundException($"Unknown content ID '{id}'.");
    public AssetRecord GetAsset(string id) => _assets.TryGetValue(id, out var value)
        ? value
        : throw new KeyNotFoundException($"Unknown asset ID '{id}'.");
    public AtlasRegion GetRegion(string id) => _regions.TryGetValue(id, out var value)
        ? value
        : throw new KeyNotFoundException($"Unknown atlas region ID '{id}'.");
}

public static partial class MvpContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static RuntimeContentCatalog LoadAndValidate(
        string sourceRoot,
        string definitionsRoot,
        string manifestRelativePath = "data/asset-manifest.json")
    {
        var issues = new List<ValidationIssue>();
        var source = Path.GetFullPath(sourceRoot);
        var definitions = Path.GetFullPath(definitionsRoot);
        var manifest = Read<AssetManifestV1>(
            ContentValidator.ResolveUnderRoot(source, manifestRelativePath), issues, "manifest.invalid");
        var catalogPath = ContentValidator.ResolveUnderRoot(definitions, "mvp-catalog.json");
        var catalog = Read<CatalogDocument>(catalogPath, issues, "catalog.invalid");

        if (manifest is null || catalog is null)
            throw new ContentValidationException(issues);
        if (manifest.SchemaVersion != ContractVersions.Content || catalog.SchemaVersion != ContractVersions.Content)
            issues.Add(new("content.version", $"Expected content schema {ContractVersions.Content}."));
        if (manifest.BuildVersion < 1 || string.IsNullOrWhiteSpace(catalog.CatalogVersion))
            issues.Add(new("content.version", "Build and catalog versions are required."));

        ValidateAssets(source, manifest.Assets ?? [], issues);
        ValidateDefinitions(catalog.Definitions ?? [], issues);
        if (catalog.CatalogVersion.StartsWith("mvp-", StringComparison.Ordinal))
        {
            ValidateRequiredCatalogIds(catalog.Definitions ?? [], issues);
            ValidateRequiredVisualAssets(manifest.Assets ?? [], issues);
        }

        var atlases = new List<AtlasDocument>();
        foreach (var asset in (manifest.Assets ?? []).Where(item => item.Kind == "atlas"))
        {
            if (string.IsNullOrWhiteSpace(asset.Metadata))
            {
                issues.Add(new("atlas.metadata", $"Atlas '{asset.Id}' has no metadata."));
                continue;
            }
            var metadataPath = TryResolve(source, asset.Metadata, asset.Id, issues);
            if (metadataPath is null)
                continue;
            var atlas = Read<AtlasDocument>(metadataPath, issues, "atlas.invalid");
            if (atlas is not null)
                atlases.Add(atlas);
        }
        ValidateAtlases(atlases, manifest.Assets ?? [], issues);
        ValidateCrossReferences(catalog.Definitions ?? [], manifest.Assets ?? [], atlases, issues);
        ValidateResearchGraph(catalog.Definitions ?? [], issues);

        if (issues.Count > 0)
            throw new ContentValidationException(issues);

        var fingerprint = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(catalogPath))).ToLowerInvariant();
        return new RuntimeContentCatalog(
            catalog,
            manifest.Assets ?? [],
            atlases.SelectMany(atlas => atlas.Regions),
            fingerprint);
    }

    /// <summary>
    /// P1-owned generated-root gate: every authored manifest ID still resolves under the
    /// authoring root, data/metadata sources are present in the generated root, and
    /// texture/atlas/sound kinds also have compiled <c>{id}.xnb</c> outputs.
    /// Does not soften P0 <see cref="ContentValidator"/> / <see cref="ContentBuildPlan"/> semantics.
    /// </summary>
    public static void ValidateGeneratedRoot(
        string generatedRoot,
        string sourceRoot,
        string definitionsRoot,
        string manifestRelativePath = "data/asset-manifest.json")
    {
        var catalog = LoadAndValidate(sourceRoot, definitionsRoot, manifestRelativePath);
        var issues = new List<ValidationIssue>();
        var generated = Path.GetFullPath(generatedRoot);
        var source = Path.GetFullPath(sourceRoot);
        var manifest = Read<AssetManifestV1>(
            ContentValidator.ResolveUnderRoot(source, manifestRelativePath), issues, "manifest.invalid")
            ?? throw new ContentValidationException(issues);

        _ = ContentBuildRules.Enumerate(manifest);

        foreach (var asset in manifest.Assets ?? [])
        {
            if (!catalog.AssetIds.Contains(asset.Id))
                issues.Add(new("generated.missing-catalog-id", $"Generated validation missing catalog asset '{asset.Id}'."));

            var authored = TryResolve(source, asset.Source, asset.Id, issues);
            if (authored is null || !File.Exists(authored))
                issues.Add(new("asset.missing-source", $"Asset '{asset.Id}' authored source '{asset.Source}' is missing."));

            var generatedSource = TryResolve(generated, asset.Source, asset.Id, issues);
            if (generatedSource is null || !File.Exists(generatedSource))
                issues.Add(new("generated.missing-source",
                    $"Asset '{asset.Id}' source '{asset.Source}' is missing under generated root."));

            if (asset.Kind is "atlas" or "texture" or "sound")
            {
                var compiledRelative = asset.Id.Replace('\\', '/') + ".xnb";
                var compiled = TryResolve(generated, compiledRelative, asset.Id, issues);
                if (compiled is null || !File.Exists(compiled))
                    issues.Add(new("generated.missing-xnb",
                        $"Asset '{asset.Id}' compiled artifact '{compiledRelative}' is missing."));
            }
        }

        if (issues.Count > 0)
            throw new ContentValidationException(issues);
    }

    private static void ValidateAssets(string root, IReadOnlyList<AssetRecord> assets, List<ValidationIssue> issues)
    {
        AddDuplicates(assets.Select(item => item.Id), "asset.duplicate-id", issues);
        AddDuplicates(assets.Select(item => item.Source.ToLowerInvariant()), "asset.duplicate-source", issues);
        foreach (var asset in assets)
        {
            if (!SlashId().IsMatch(asset.Id))
                issues.Add(new("asset.id", $"Asset ID '{asset.Id}' must be lowercase slash-separated."));
            if (asset.Status is not ("candidate" or "approved" or "placeholder"))
                issues.Add(new("asset.status", $"Asset '{asset.Id}' has invalid status '{asset.Status}'."));
            if (asset.Owner != "P1_CONTENT_ART" && asset.Owner != "P0_FOUNDATION")
                issues.Add(new("asset.owner", $"Asset '{asset.Id}' has unknown owner '{asset.Owner}'."));
            if (string.IsNullOrWhiteSpace(asset.License) || string.IsNullOrWhiteSpace(asset.Provenance) ||
                string.IsNullOrWhiteSpace(asset.Attribution))
                issues.Add(new("asset.provenance", $"Asset '{asset.Id}' lacks complete provenance/license."));
            if (asset.Status is "candidate" or "placeholder" &&
                (string.IsNullOrWhiteSpace(asset.Waiver) || string.IsNullOrWhiteSpace(asset.ReplacementCriterion)))
                issues.Add(new("asset.waiver", $"Asset '{asset.Id}' lacks candidate waiver/replacement criterion."));

            var path = TryResolve(root, asset.Source, asset.Id, issues);
            if (path is null || !File.Exists(path))
            {
                issues.Add(new("asset.missing-source", $"Asset '{asset.Id}' source '{asset.Source}' is missing."));
                continue;
            }
            if (!string.IsNullOrWhiteSpace(asset.SourceHash))
            {
                var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
                if (!actual.Equals(asset.SourceHash, StringComparison.Ordinal))
                    issues.Add(new("asset.hash", $"Asset '{asset.Id}' source hash does not match."));
            }
            if (asset.Kind is "atlas" or "texture")
                ValidatePng(path, asset, issues);
        }
    }

    private static void ValidatePng(string path, AssetRecord asset, List<ValidationIssue> issues)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[24];
        if (stream.Read(header) != header.Length || !header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            issues.Add(new("texture.png", $"Asset '{asset.Id}' is not a valid PNG."));
            return;
        }
        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
        if (width != asset.Width || height != asset.Height || width > 2048 || height > 2048)
            issues.Add(new("texture.dimensions", $"Asset '{asset.Id}' dimensions are {width}x{height}, expected {asset.Width}x{asset.Height}."));
    }

    private static void ValidateDefinitions(IReadOnlyList<CatalogDefinition> definitions, List<ValidationIssue> issues)
    {
        AddDuplicates(definitions.Select(item => item.Id), "catalog.duplicate-id", issues);
        foreach (var definition in definitions)
        {
            if (!CanonicalId().IsMatch(definition.Id))
                issues.Add(new("catalog.id", $"Definition ID '{definition.Id}' is not canonical."));
            foreach (var pair in definition.Values ?? new Dictionary<string, double>())
                if (!double.IsFinite(pair.Value) || pair.Value < 0)
                    issues.Add(new("catalog.range", $"'{definition.Id}.{pair.Key}' must be finite and nonnegative."));
        }
    }

    private static void ValidateAtlases(
        IReadOnlyList<AtlasDocument> atlases,
        IReadOnlyList<AssetRecord> assets,
        List<ValidationIssue> issues)
    {
        var assetIds = assets.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var regionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var atlas in atlases)
        {
            if (!assetIds.Contains(atlas.TextureAssetId) || atlas.Width > 2048 || atlas.Height > 2048 ||
                atlas.Padding != 2 || atlas.Extrusion != 1 || atlas.RotatedPacking)
                issues.Add(new("atlas.rules", $"Atlas '{atlas.TextureAssetId}' violates packing rules."));
            var collisionIds = atlas.Collisions.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var region in atlas.Regions)
            {
                if (!regionIds.Add(region.Id))
                    issues.Add(new("atlas.duplicate-region", $"Duplicate atlas region '{region.Id}'."));
                if (!SlashId().IsMatch(region.Id) || region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0 ||
                    region.X + region.Width > atlas.Width || region.Y + region.Height > atlas.Height)
                    issues.Add(new("atlas.region", $"Atlas region '{region.Id}' has invalid ID or bounds."));
                if (region.PivotX is not 0.5 || region.PivotY is not 0.5)
                    issues.Add(new("atlas.pivot", $"Atlas region '{region.Id}' must use pivot (0.5,0.5)."));
                if (region.Collision is not null && !collisionIds.Contains(region.Collision))
                    issues.Add(new("atlas.collision", $"Region '{region.Id}' references missing collision '{region.Collision}'."));
                if (region.Animation is not null)
                {
                    // Frame count 1 = honest single-pose placeholder (no packed strip yet).
                    // Counts 2–8 require a packed horizontal strip of equal cell width.
                    if (region.Animation.Fps is not (6 or 8 or 12) ||
                        region.Animation.Frames.Count is < 1 or > 8)
                        issues.Add(new("atlas.animation", $"Region '{region.Id}' has invalid animation metadata."));
                    else if (region.Animation.Frames.Count > 1)
                    {
                        if (region.Width % region.Animation.Frames.Count != 0)
                            issues.Add(new("atlas.animation-strip",
                                $"Region '{region.Id}' claims {region.Animation.Frames.Count} frames but width {region.Width} is not divisible into equal cells."));
                    }
                }
                foreach (var point in region.Hardpoints?.Values ?? [])
                    if (point.X < 0 || point.Y < 0 || point.X >= region.Width || point.Y >= region.Height)
                        issues.Add(new("atlas.hardpoint", $"Region '{region.Id}' has an out-of-bounds hardpoint."));
                ValidateFrameTier(region, issues);
            }

            if (atlas.TextureAssetId == "atlases/player-modules")
            {
                var wayfarer = atlas.Regions.FirstOrDefault(region => region.Id == "ships/player/wayfarer");
                if (wayfarer?.Hardpoints is null ||
                    !wayfarer.Hardpoints.ContainsKey("primaryWeapon") ||
                    !wayfarer.Hardpoints.ContainsKey("miningTool") ||
                    !wayfarer.Hardpoints.ContainsKey("utility") ||
                    !wayfarer.Hardpoints.ContainsKey("leftEngine") ||
                    !wayfarer.Hardpoints.ContainsKey("rightEngine") ||
                    !wayfarer.Hardpoints.ContainsKey("shieldOrigin"))
                    issues.Add(new("atlas.hardpoint", "Wayfarer hardpoints are incomplete."));
            }
        }
    }

    private static void ValidateFrameTier(AtlasRegion region, List<ValidationIssue> issues)
    {
        var valid = region.Id switch
        {
            "ships/player/wayfarer" => region.Width == 64 && region.Height == 64,
            var id when id.StartsWith("enemies/", StringComparison.Ordinal) =>
                region.Width is >= 32 and <= 64 && region.Height is >= 32 and <= 64,
            var id when id.StartsWith("asteroids/small/", StringComparison.Ordinal) =>
                region.Width == 32 && region.Height == 32,
            var id when id.StartsWith("asteroids/medium/", StringComparison.Ordinal) =>
                region.Width == 64 && region.Height == 64,
            var id when id.StartsWith("asteroids/large/", StringComparison.Ordinal) =>
                region.Width == 96 && region.Height == 96,
            var id when id.StartsWith("pickups/", StringComparison.Ordinal) =>
                region.Width is >= 8 and <= 12 && region.Height is >= 8 and <= 12,
            var id when id.StartsWith("ui/icons/", StringComparison.Ordinal) =>
                (region.Width == 24 || region.Width == 32) && region.Height == region.Width,
            _ => true
        };
        if (!valid)
            issues.Add(new("atlas.frame-tier", $"Region '{region.Id}' violates documented frame tier ({region.Width}x{region.Height})."));
    }

    private static void ValidateRequiredCatalogIds(IReadOnlyList<CatalogDefinition> definitions, List<ValidationIssue> issues)
    {
        string[] required =
        [
            "MAT_FERRITE", "MAT_LUMEN", "MAT_DATA_CORE",
            "ENV_CINDER_BELT", "ENV_ION_VEIL", "CAP_TRAVEL_ION_VEIL",
            "ENM_INTERCEPTOR", "ENM_GUNSHIP", "ENM_SAPPER", "MOD_ELITE_PROTOCOL",
            "MOD_WEAPON_PULSE", "MOD_WEAPON_BEAM", "MOD_WEAPON_SEEKER",
            "MOD_MINING_LASER", "MOD_MINING_CHARGE",
            "MOD_SHIELD_CAPACITOR", "MOD_SHIELD_REFLECTIVE",
            "MOD_ENGINE_VECTOR", "MOD_ENGINE_BLINK",
            "MOD_UTILITY_TRACTOR", "MOD_UTILITY_DRONE",
            "UPG_OVERCHARGED_MUNITIONS", "UPG_RAPID_CYCLING", "UPG_FORKED_OUTPUT", "UPG_PENETRATING_FIELD",
            "UPG_SHIELD_RESERVOIR", "UPG_FAST_REBOOT", "UPG_REINFORCED_FRAME", "UPG_THRUSTER_OVERCLOCK",
            "UPG_MOBILITY_LOOP", "UPG_FRACTURE_LENS", "UPG_MAGNETIC_SWEEP", "UPG_SHOCK_TRANSIT",
            "RES_HULL_REINFORCEMENT", "RES_SHIELD_REFLECTIVE", "RES_WEAPON_BEAM", "RES_WEAPON_SEEKER",
            "RES_MINING_SEISMIC", "RES_MINING_ASSAY", "RES_ENGINE_TUNING", "RES_ENGINE_BLINK",
            "RES_UTILITY_DRONE", "RES_TRACTOR_CALIBRATION", "RES_NAV_ION_VEIL", "RES_RECOVERY_PROTOCOLS",
            "OBJ_FIELD_PROOF", "EXT_STANDARD_GATE"
        ];
        var ids = definitions.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in required)
            if (!ids.Contains(id))
                issues.Add(new("catalog.missing-required", $"MVP catalog is missing required ID '{id}'."));
    }

    private static void ValidateRequiredVisualAssets(IReadOnlyList<AssetRecord> assets, List<ValidationIssue> issues)
    {
        string[] required =
        [
            "atlases/player-modules", "atlases/enemies-telegraphs", "atlases/asteroids-resources", "atlases/ui-icons",
            "backgrounds/cinder-belt", "backgrounds/ion-veil", "art/contact-sheet", "audio/essential-cues"
        ];
        var ids = assets.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in required)
            if (!ids.Contains(id))
                issues.Add(new("asset.missing-required", $"Manifest is missing required visual/audio asset '{id}'."));
        if (assets.Any(item => item.Kind == "sound" && item.Id.Contains("music", StringComparison.OrdinalIgnoreCase)))
            issues.Add(new("asset.music-forbidden", "MVP must not include music files."));
    }

    private static void ValidateCrossReferences(
        IReadOnlyList<CatalogDefinition> definitions,
        IReadOnlyList<AssetRecord> assets,
        IReadOnlyList<AtlasDocument> atlases,
        List<ValidationIssue> issues)
    {
        var ids = definitions.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var visualIds = atlases.SelectMany(item => item.Regions).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var assetIds = assets.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var definition in definitions)
        foreach (var reference in definition.References ?? [])
            if (!ids.Contains(reference) && !visualIds.Contains(reference) && !assetIds.Contains(reference))
                issues.Add(new("catalog.missing-reference", $"'{definition.Id}' references missing ID '{reference}'."));
    }

    private static void ValidateResearchGraph(IReadOnlyList<CatalogDefinition> definitions, List<ValidationIssue> issues)
    {
        var research = definitions.Where(item => item.Kind == "research").ToDictionary(item => item.Id, StringComparer.Ordinal);
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (state.GetValueOrDefault(id) == 1)
                return false;
            if (state.GetValueOrDefault(id) == 2)
                return true;
            state[id] = 1;
            foreach (var dependency in research[id].References?.Where(research.ContainsKey) ?? [])
                if (!Visit(dependency))
                    return false;
            state[id] = 2;
            return true;
        }
        foreach (var id in research.Keys)
            if (!Visit(id))
            {
                issues.Add(new("catalog.research-cycle", $"Research graph contains a cycle at '{id}'."));
                break;
            }
    }

    private static T? Read<T>(string path, List<ValidationIssue> issues, string code)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
                ?? throw new JsonException("Document is empty.");
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            issues.Add(new(code, $"{path}: {exception.Message}"));
            return default;
        }
    }

    private static string? TryResolve(string root, string path, string id, List<ValidationIssue> issues)
    {
        try
        {
            return ContentValidator.ResolveUnderRoot(root, path);
        }
        catch (InvalidOperationException exception)
        {
            issues.Add(new("asset.path", $"Asset '{id}': {exception.Message}"));
            return null;
        }
    }

    private static void AddDuplicates(IEnumerable<string> values, string code, List<ValidationIssue> issues)
    {
        foreach (var duplicate in values.GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1))
            issues.Add(new(code, $"Duplicate value '{duplicate.Key}'."));
    }

    [GeneratedRegex("^[a-z0-9]+(?:[a-z0-9-]*/)*[a-z0-9][a-z0-9-]*$")]
    private static partial Regex SlashId();

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,127}$")]
    private static partial Regex CanonicalId();
}
