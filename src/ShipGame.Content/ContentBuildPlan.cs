using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content;

public static class ContentBuildPlan
{
    public static IReadOnlyList<string> DataSources(AssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Assets is null)
            throw new ContentValidationException([new("manifest.assets-required", "Manifest assets must be an array.")]);
        var sources = new List<string> { "data/asset-manifest.json" };
        foreach (var asset in manifest.Assets.OrderBy(asset => asset.Id, StringComparer.Ordinal))
        {
            if (!string.Equals(asset.Kind, "data", StringComparison.Ordinal))
                throw new ContentValidationException(
                    [new("asset.unsupported-build-kind", $"P0 builder has no reviewed rule for kind '{asset.Kind}' ({asset.Id}).")]);
            sources.Add(asset.Source.Replace('\\', '/'));
        }
        return sources;
    }
}
