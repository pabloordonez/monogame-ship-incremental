using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Simulation;

public readonly record struct CorridorDescriptor(GridPoint From, GridPoint To);
