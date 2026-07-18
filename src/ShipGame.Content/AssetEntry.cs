using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed record AssetEntry(
    string Id,
    string Kind,
    string Source,
    string Status,
    string Owner,
    string License,
    string? Attribution = null,
    string? SourceUrl = null);
