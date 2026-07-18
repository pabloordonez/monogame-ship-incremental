using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Simulation;

public readonly record struct HazardDescriptor(
    long WarningTick,
    long ResolveTick,
    int Damage,
    GridPoint Center,
    int Radius,
    int Direction);
