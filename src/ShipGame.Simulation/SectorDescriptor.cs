using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Simulation;

public readonly record struct SectorDescriptor(SectorKind Kind, GridPoint Center);
