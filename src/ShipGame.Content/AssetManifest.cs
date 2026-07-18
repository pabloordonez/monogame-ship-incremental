using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed record AssetManifest(int SchemaVersion, IReadOnlyList<AssetEntry> Assets);
