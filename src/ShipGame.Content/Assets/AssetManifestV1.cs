using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed record AssetManifestV1(int SchemaVersion, int BuildVersion, IReadOnlyList<AssetRecord> Assets);
