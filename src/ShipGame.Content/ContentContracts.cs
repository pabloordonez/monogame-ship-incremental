using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed record AssetManifest(int SchemaVersion, IReadOnlyList<AssetEntry> Assets);

public sealed record AssetEntry(
    string Id,
    string Kind,
    string Source,
    string Status,
    string Owner,
    string License,
    string? Attribution = null,
    string? SourceUrl = null);

public sealed record ContentDefinition(ContentId Id, IReadOnlyList<ContentId>? References = null);

public sealed record ValidationIssue(string Code, string Message);

public sealed class ContentValidationException(IReadOnlyList<ValidationIssue> issues)
    : Exception(string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}")))
{
    public IReadOnlyList<ValidationIssue> Issues { get; } = issues;
}

public static class ContentValidator
{
    public static AssetManifest LoadAndValidateManifest(string sourceRoot, string manifestPath)
    {
        var root = Path.GetFullPath(sourceRoot);
        var manifestFullPath = ResolveUnderRoot(root, manifestPath);
        var manifest = JsonSerializer.Deserialize<AssetManifest>(
            File.ReadAllText(manifestFullPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ContentValidationException([new("manifest.invalid", "Manifest JSON is empty.")]);

        var issues = new List<ValidationIssue>();
        if (manifest.SchemaVersion != ContractVersions.Content)
            issues.Add(new("manifest.version", $"Expected schema {ContractVersions.Content}."));
        if (manifest.Assets is null)
            throw new ContentValidationException([new("manifest.assets-required", "Manifest assets must be an array.")]);
        var assets = manifest.Assets.Where(asset => asset is not null).ToArray();
        if (assets.Length != manifest.Assets.Count)
            issues.Add(new("asset.invalid", "Manifest assets cannot contain null entries."));

        foreach (var duplicate in assets.GroupBy(asset => asset.Id, StringComparer.Ordinal).Where(group => group.Count() > 1))
            issues.Add(new("asset.duplicate-id", $"Duplicate asset ID '{duplicate.Key}'."));

        foreach (var duplicate in assets.GroupBy(asset => asset.Source, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            issues.Add(new("asset.duplicate-source", $"Duplicate asset source '{duplicate.Key}'."));

        foreach (var asset in assets)
        {
            try
            {
                _ = new ContentId(asset.Id);
                if (string.IsNullOrWhiteSpace(asset.Kind) ||
                    string.IsNullOrWhiteSpace(asset.Source) ||
                    string.IsNullOrWhiteSpace(asset.Status))
                    throw new ArgumentException("Asset kind, source, and status are required.");
                if (!AssetArtifactExists(root, asset))
                    issues.Add(new("asset.missing-source", $"Asset '{asset.Id}' source '{asset.Source}' is missing."));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                issues.Add(new("asset.invalid", $"Asset '{asset.Id}': {exception.Message}"));
            }
            if (string.IsNullOrWhiteSpace(asset.License) || string.IsNullOrWhiteSpace(asset.Owner))
                issues.Add(new("asset.provenance", $"Asset '{asset.Id}' lacks owner or license."));
        }

        ThrowIfAny(issues);
        return manifest;
    }

    public static void ValidateDefinitions(IEnumerable<ContentDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        var issues = materialized
            .GroupBy(definition => definition.Id)
            .Where(group => group.Count() > 1)
            .Select(group => new ValidationIssue("content.duplicate-id", $"Duplicate content ID '{group.Key}'."))
            .ToList();
        var ids = materialized.Select(definition => definition.Id).ToHashSet();
        foreach (var definition in materialized)
        foreach (var reference in definition.References ?? [])
            if (!ids.Contains(reference))
                issues.Add(new("content.missing-reference", $"'{definition.Id}' references missing ID '{reference}'."));
        ThrowIfAny(issues);
    }

    public static string ResolveUnderRoot(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new InvalidOperationException("Rooted content paths are forbidden.");
        var rooted = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!rooted.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Content path escapes its configured root.");
        return rooted;
    }

    /// <summary>
    /// Authoring roots resolve <see cref="AssetEntry.Source"/>; runtime generated roots may
    /// instead contain compiled <c>{id}.xnb</c> artifacts for texture/atlas/sound kinds.
    /// </summary>
    public static bool AssetArtifactExists(string root, AssetEntry asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var source = ResolveUnderRoot(root, asset.Source);
        if (File.Exists(source))
            return true;
        if (asset.Kind is not ("atlas" or "texture" or "sound"))
            return false;
        var compiled = ResolveUnderRoot(root, asset.Id.Replace('\\', '/') + ".xnb");
        return File.Exists(compiled);
    }

    private static void ThrowIfAny(List<ValidationIssue> issues)
    {
        if (issues.Count > 0)
            throw new ContentValidationException(issues);
    }
}

public static class ContentBuildPlan
{
    /// <summary>
    /// P0 data-copy subset. Texture/atlas/sound/metadata rules live in <see cref="ContentBuildRules"/>.
    /// Non-data kinds are skipped here so mixed manifests remain compatible.
    /// </summary>
    public static IReadOnlyList<string> DataSources(AssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Assets is null)
            throw new ContentValidationException([new("manifest.assets-required", "Manifest assets must be an array.")]);
        var sources = new List<string> { "data/asset-manifest.json" };
        foreach (var asset in manifest.Assets.OrderBy(asset => asset.Id, StringComparer.Ordinal))
        {
            if (!string.Equals(asset.Kind, "data", StringComparison.Ordinal))
                continue;
            sources.Add(asset.Source.Replace('\\', '/'));
        }
        return sources;
    }
}

public interface IAssetCatalog
{
    string LoadText(ContentId id);
}

public sealed class FileAssetCatalog(string root, AssetManifest manifest) : IAssetCatalog
{
    private readonly Dictionary<string, AssetEntry> _entries =
        manifest.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal);

    public string LoadText(ContentId id)
    {
        if (!_entries.TryGetValue(id.Value, out var entry))
            throw new KeyNotFoundException($"Unknown asset '{id}'.");
        return File.ReadAllText(ContentValidator.ResolveUnderRoot(root, entry.Source));
    }
}
