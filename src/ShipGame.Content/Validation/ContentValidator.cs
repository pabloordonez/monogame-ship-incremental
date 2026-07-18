using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content;

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
                var source = ResolveUnderRoot(root, asset.Source);
                if (!File.Exists(source))
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

    private static void ThrowIfAny(List<ValidationIssue> issues)
    {
        if (issues.Count > 0)
            throw new ContentValidationException(issues);
    }
}
