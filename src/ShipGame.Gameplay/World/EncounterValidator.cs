using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Gameplay;

public static class EncounterValidator
{
    public static EncounterValidationResult Validate(FieldDescriptor? descriptor)
    {
        if (descriptor is null)
            return new(false, ["Descriptor is required."]);
        var issues = new List<string>();
        if (descriptor.Sectors.Count != 6 ||
            descriptor.Sectors.Count(sector => sector.Kind == SectorKind.Spawn) != 1 ||
            descriptor.Sectors.Count(sector => sector.Kind == SectorKind.Objective) != 3 ||
            descriptor.Sectors.Count(sector => sector.Kind == SectorKind.EliteArena) != 1 ||
            descriptor.Sectors.Count(sector => sector.Kind == SectorKind.Extraction) != 1)
            issues.Add("Required sector cardinality is invalid.");
        if (descriptor.AsteroidCells.Count > FieldDescriptor.MaximumAsteroidCells)
            issues.Add("Asteroid cell bound exceeded.");
        if (descriptor.Hazards.Count > FieldDescriptor.MaximumHazards)
            issues.Add("Hazard bound exceeded.");
        if (descriptor.AsteroidCells.GroupBy(cell => cell.CellId).Any(group => group.Count() != 1))
            issues.Add("Asteroid cell IDs must be unique.");
        if (descriptor.AsteroidCells.Sum(cell => cell.Kind == AsteroidCellKind.Ferrite ? 2 : 0) < 30)
            issues.Add("Guaranteed Ferrite cannot satisfy the objective.");
        if (descriptor.Sectors.Any(sector => !InBounds(sector.Center)) ||
            descriptor.AsteroidCells.Any(cell => !InBounds(cell.Position)))
            issues.Add("Descriptor contains out-of-bounds positions.");
        if (descriptor.Sectors.Count > 0 && !RequiredSectorsReachable(descriptor))
            issues.Add("Required sectors are not reachable with player clearance.");
        if (descriptor.Sectors.Count(sector => sector.Kind == SectorKind.Spawn) == 1 &&
            descriptor.Sectors.Count(sector => sector.Kind == SectorKind.Extraction) == 1)
        {
            var spawn = descriptor.Sectors.Single(sector => sector.Kind == SectorKind.Spawn).Center;
            var extraction = descriptor.Sectors.Single(sector => sector.Kind == SectorKind.Extraction).Center;
            var dx = (spawn.X - extraction.X) * FieldDescriptor.WorldUnitsPerCell;
            var dy = (spawn.Y - extraction.Y) * FieldDescriptor.WorldUnitsPerCell;
            if ((long)dx * dx + (long)dy * dy < 700L * 700L)
                issues.Add("Extraction is less than 700 world units from spawn.");
        }
        return new(issues.Count == 0, issues.AsReadOnly());
    }

    private static bool RequiredSectorsReachable(FieldDescriptor descriptor)
    {
        var walkable = new bool[FieldDescriptor.Width, FieldDescriptor.Height];
        foreach (var corridor in descriptor.Corridors)
        foreach (var point in EncounterGenerator.Rasterize(corridor))
        for (var y = point.Y - 1; y <= point.Y + 1; y++)
        for (var x = point.X - 1; x <= point.X + 1; x++)
            if (InBounds(new(x, y)))
                walkable[x, y] = true;
        foreach (var sector in descriptor.Sectors)
        for (var y = sector.Center.Y - 2; y <= sector.Center.Y + 2; y++)
        for (var x = sector.Center.X - 2; x <= sector.Center.X + 2; x++)
            if (InBounds(new(x, y)))
                walkable[x, y] = true;
        foreach (var asteroid in descriptor.AsteroidCells)
            walkable[asteroid.Position.X, asteroid.Position.Y] = false;

        var spawn = descriptor.Sectors.FirstOrDefault(sector => sector.Kind == SectorKind.Spawn).Center;
        if (!InBounds(spawn) || !walkable[spawn.X, spawn.Y])
            return false;
        var visited = new bool[FieldDescriptor.Width, FieldDescriptor.Height];
        var queue = new Queue<GridPoint>();
        queue.Enqueue(spawn);
        visited[spawn.X, spawn.Y] = true;
        var directions = new[] { new GridPoint(1, 0), new GridPoint(-1, 0), new GridPoint(0, 1), new GridPoint(0, -1) };
        while (queue.TryDequeue(out var point))
        foreach (var direction in directions)
        {
            var next = new GridPoint(point.X + direction.X, point.Y + direction.Y);
            if (InBounds(next) && walkable[next.X, next.Y] && !visited[next.X, next.Y])
            {
                visited[next.X, next.Y] = true;
                queue.Enqueue(next);
            }
        }
        return descriptor.Sectors.All(sector => visited[sector.Center.X, sector.Center.Y]);
    }

    private static bool InBounds(GridPoint point) =>
        point.X >= 0 && point.X < FieldDescriptor.Width &&
        point.Y >= 0 && point.Y < FieldDescriptor.Height;
}
