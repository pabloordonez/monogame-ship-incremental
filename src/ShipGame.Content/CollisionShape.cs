using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed record CollisionShape(string Id, string Kind, IReadOnlyList<double> Values);
