using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed record CatalogDefinition(
    string Id,
    string Kind,
    IReadOnlyList<string>? References,
    IReadOnlyDictionary<string, double>? Values);
