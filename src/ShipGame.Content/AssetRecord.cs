using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed record AssetRecord(
    string Id,
    string Kind,
    string Source,
    string Status,
    string Owner,
    string License,
    string Attribution,
    string? SourceUrl,
    string Provenance,
    string ReplacementCriterion,
    string? Waiver,
    int? Width,
    int? Height,
    string? Metadata,
    string? SourceHash);
