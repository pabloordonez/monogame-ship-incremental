using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed record EncounterValidationResult(bool IsValid, IReadOnlyList<string> Issues);
