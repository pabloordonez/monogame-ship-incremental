using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipGame.Domain;

namespace ShipGame.Content;

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
