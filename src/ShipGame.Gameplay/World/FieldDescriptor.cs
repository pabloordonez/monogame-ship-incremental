using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record FieldDescriptor
{
    public const int Width = 64;
    public const int Height = 48;
    public const int WorldUnitsPerCell = 50;
    public const int MaximumAsteroidCells = 160;
    public const int MaximumHazards = 64;

    public required GenerationIdentity Identity { get; init; }
    public required int Attempt { get; init; }
    public required IReadOnlyList<SectorDescriptor> Sectors { get; init; }
    public required IReadOnlyList<CorridorDescriptor> Corridors { get; init; }
    public required IReadOnlyList<AsteroidCellDescriptor> AsteroidCells { get; init; }
    public required IReadOnlyList<HazardDescriptor> Hazards { get; init; }

    public SectorDescriptor Spawn => Sectors.Single(sector => sector.Kind == SectorKind.Spawn);
    public SectorDescriptor EliteArena => Sectors.Single(sector => sector.Kind == SectorKind.EliteArena);
    public SectorDescriptor Extraction => Sectors.Single(sector => sector.Kind == SectorKind.Extraction);
}
