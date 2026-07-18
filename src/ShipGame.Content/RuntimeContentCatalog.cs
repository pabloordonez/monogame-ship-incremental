using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipGame.Domain;

namespace ShipGame.Content;

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
