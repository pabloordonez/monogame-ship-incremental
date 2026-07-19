using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Gameplay;

public readonly record struct CorridorDescriptor(GridPoint From, GridPoint To);
