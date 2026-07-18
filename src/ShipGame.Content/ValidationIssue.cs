using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed record ValidationIssue(string Code, string Message);
