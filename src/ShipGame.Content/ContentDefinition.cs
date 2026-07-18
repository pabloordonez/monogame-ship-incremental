using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed record ContentDefinition(ContentId Id, IReadOnlyList<ContentId>? References = null);
