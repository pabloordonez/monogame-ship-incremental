using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipGame.Domain;

namespace ShipGame.Content;

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
