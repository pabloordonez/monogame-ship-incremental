using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content;

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
