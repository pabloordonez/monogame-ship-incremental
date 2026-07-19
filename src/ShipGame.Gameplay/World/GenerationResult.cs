using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record GenerationResult(FieldDescriptor Descriptor, bool FallbackUsed);
