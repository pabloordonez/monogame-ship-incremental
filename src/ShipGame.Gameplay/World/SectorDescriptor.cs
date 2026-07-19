using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Gameplay;

public readonly record struct SectorDescriptor(SectorKind Kind, GridPoint Center);
