using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed record GenerationResult(FieldDescriptor Descriptor, bool FallbackUsed);
