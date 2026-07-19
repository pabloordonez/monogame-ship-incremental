using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record EncounterValidationResult(bool IsValid, IReadOnlyList<string> Issues);
