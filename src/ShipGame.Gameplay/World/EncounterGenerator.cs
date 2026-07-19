using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed class EncounterGenerator
{
    public const int CurrentGenerationVersion = 1;
    private const int MaximumAttempts = 4;

    /// <param name="invalidatePrimaryForTest">
    /// Forces attempt 0 invalid so soft retry (Attempt 1–3) can be exercised in tests.
    /// </param>
    /// <param name="invalidateAllAttemptsForTest">
    /// Forces attempts 0–3 invalid so the hard GenerateFallback path (Attempt == 4) is exercised in tests.
    /// </param>
    public GenerationResult Generate(
        GenerationIdentity identity,
        bool invalidatePrimaryForTest = false,
        bool invalidateAllAttemptsForTest = false)
    {
        ValidateIdentity(identity);
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var descriptor = GenerateAttempt(identity, attempt);
            if (invalidateAllAttemptsForTest || (attempt == 0 && invalidatePrimaryForTest))
                descriptor = descriptor with { Corridors = Array.Empty<CorridorDescriptor>() };
            if (EncounterValidator.Validate(descriptor).IsValid)
                return new(descriptor, attempt > 0);
        }

        var fallback = GenerateFallback(identity);
        var validation = EncounterValidator.Validate(fallback);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Deterministic fallback was invalid: {string.Join("; ", validation.Issues)}");
        return new(fallback, true);
    }

    private static FieldDescriptor GenerateAttempt(GenerationIdentity identity, int attempt)
    {
        var random = CreateAttemptRandom(identity, attempt);
        var spawn = new GridPoint(4, FieldDescriptor.Height / 2);
        var objectives = new[]
        {
            new GridPoint(18 + NextInt(random, 0, 4), 8 + NextInt(random, 0, 5)),
            new GridPoint(30 + NextInt(random, 0, 5), 34 + NextInt(random, 0, 5)),
            new GridPoint(43 + NextInt(random, 0, 4), 10 + NextInt(random, 0, 6))
        };
        var elite = new GridPoint(52, 35);
        var extraction = new GridPoint(59, 6);
        var sectors = new List<SectorDescriptor>
        {
            new(SectorKind.Spawn, spawn),
            new(SectorKind.Objective, objectives[0]),
            new(SectorKind.Objective, objectives[1]),
            new(SectorKind.Objective, objectives[2]),
            new(SectorKind.EliteArena, elite),
            new(SectorKind.Extraction, extraction)
        };
        var corridors = new List<CorridorDescriptor>();
        var previous = spawn;
        foreach (var destination in objectives.Append(elite).Append(extraction))
        {
            corridors.Add(new(previous, new(destination.X, previous.Y)));
            corridors.Add(new(new(destination.X, previous.Y), destination));
            previous = destination;
        }

        var blocked = BuildProtectedCells(sectors, corridors);
        var asteroidCount = identity.EnvironmentId == WorldRunIds.CinderBelt ? 112 : 72;
        var asteroids = new List<AsteroidCellDescriptor>(asteroidCount);
        var occupied = new HashSet<GridPoint>(blocked);
        var ferriteCells = 0;
        for (var cellId = 0; cellId < asteroidCount; cellId++)
        {
            GridPoint position;
            var placementAttempts = 0;
            do
            {
                position = new(2 + NextInt(random, 0, FieldDescriptor.Width - 4), 2 + NextInt(random, 0, FieldDescriptor.Height - 4));
                placementAttempts++;
            } while (!occupied.Add(position) && placementAttempts < 512);
            if (placementAttempts >= 512)
                break;

            // Ordinary cells provide cover/topology. Resource-bearing cells use catalog weights.
            AsteroidCellKind kind;
            if (NextInt(random, 0, 100) < 55)
            {
                kind = AsteroidCellKind.Ordinary;
            }
            else
            {
                var lumenPercent = identity.EnvironmentId == WorldRunIds.CinderBelt ? 15 : 30;
                kind = NextInt(random, 0, 100) < lumenPercent
                    ? AsteroidCellKind.Lumen
                    : AsteroidCellKind.Ferrite;
            }
            if (kind == AsteroidCellKind.Ferrite)
                ferriteCells++;
            asteroids.Add(new(
                cellId,
                position,
                kind,
                kind == AsteroidCellKind.Ordinary ? 60 : 45,
                identity.EnvironmentId == WorldRunIds.CinderBelt && NextInt(random, 0, 4) == 0));
        }

        EnsureObjectiveFerrite(asteroids, ferriteCells);
        return CreateDescriptor(identity, attempt, sectors, corridors, asteroids, BuildHazards(identity, random));
    }

    private static FieldDescriptor GenerateFallback(GenerationIdentity identity)
    {
        var sectors = new[]
        {
            new SectorDescriptor(SectorKind.Spawn, new(4, 24)),
            new SectorDescriptor(SectorKind.Objective, new(18, 24)),
            new SectorDescriptor(SectorKind.Objective, new(32, 24)),
            new SectorDescriptor(SectorKind.Objective, new(45, 24)),
            new SectorDescriptor(SectorKind.EliteArena, new(54, 24)),
            new SectorDescriptor(SectorKind.Extraction, new(59, 6))
        };
        var corridors = new[]
        {
            new CorridorDescriptor(new(4, 24), new(54, 24)),
            new CorridorDescriptor(new(54, 24), new(59, 24)),
            new CorridorDescriptor(new(59, 24), new(59, 6))
        };
        // Validator requires Ferrite cells * 2 >= 30 (OBJ_FIELD_PROOF floor); keep ≥15 Ferrite cells.
        var asteroids = Enumerable.Range(0, 24)
            .Select(index => new AsteroidCellDescriptor(
                index,
                new GridPoint(10 + index * 2, index % 2 == 0 ? 15 : 33),
                index < 16 ? AsteroidCellKind.Ferrite : AsteroidCellKind.Ordinary,
                45,
                identity.EnvironmentId == WorldRunIds.CinderBelt && index % 4 == 0))
            .ToArray();
        var random = CreateAttemptRandom(identity, MaximumAttempts);
        return CreateDescriptor(identity, MaximumAttempts, sectors, corridors, asteroids, BuildHazards(identity, random));
    }

    private static FieldDescriptor CreateDescriptor(
        GenerationIdentity identity,
        int attempt,
        IEnumerable<SectorDescriptor> sectors,
        IEnumerable<CorridorDescriptor> corridors,
        IEnumerable<AsteroidCellDescriptor> asteroids,
        IEnumerable<HazardDescriptor> hazards) =>
        new()
        {
            Identity = identity,
            Attempt = attempt,
            Sectors = ReadOnly(sectors),
            Corridors = ReadOnly(corridors),
            AsteroidCells = ReadOnly(asteroids),
            Hazards = ReadOnly(hazards)
        };

    private static IEnumerable<HazardDescriptor> BuildHazards(GenerationIdentity identity, Pcg32 random)
    {
        var hazards = new List<HazardDescriptor>();
        if (identity.EnvironmentId == WorldRunIds.CinderBelt)
        {
            var resolve = 60 * 60L + NextInt(random, -5 * 60, 5 * 60 + 1);
            while (hazards.Count < FieldDescriptor.MaximumHazards && resolve < WorldRun.DeadlineTick)
            {
                hazards.Add(new(resolve - 4 * 60, resolve, 25, default, 0, NextInt(random, 0, 8)));
                resolve += 75 * 60L + NextInt(random, -10 * 60, 10 * 60 + 1);
            }
        }
        else
        {
            for (var wave = 1;
                 hazards.Count < FieldDescriptor.MaximumHazards &&
                 wave * 45 * 60L < WorldRun.DeadlineTick;
                 wave++)
            for (var circle = 0; circle < 3 && hazards.Count < FieldDescriptor.MaximumHazards; circle++)
            {
                var resolve = wave * 45 * 60L;
                hazards.Add(new(
                    resolve - 150,
                    resolve,
                    30,
                    new(6 + NextInt(random, 0, FieldDescriptor.Width - 12), 6 + NextInt(random, 0, FieldDescriptor.Height - 12)),
                    3,
                    0));
            }
        }
        return hazards;
    }

    private static HashSet<GridPoint> BuildProtectedCells(
        IEnumerable<SectorDescriptor> sectors,
        IEnumerable<CorridorDescriptor> corridors)
    {
        var protectedCells = new HashSet<GridPoint>();
        foreach (var sector in sectors)
        for (var y = sector.Center.Y - 2; y <= sector.Center.Y + 2; y++)
        for (var x = sector.Center.X - 2; x <= sector.Center.X + 2; x++)
            protectedCells.Add(new(x, y));
        foreach (var corridor in corridors)
        foreach (var point in Rasterize(corridor))
        for (var y = point.Y - 1; y <= point.Y + 1; y++)
        for (var x = point.X - 1; x <= point.X + 1; x++)
            protectedCells.Add(new(x, y));
        return protectedCells;
    }

    internal static IEnumerable<GridPoint> Rasterize(CorridorDescriptor corridor)
    {
        var x = corridor.From.X;
        var y = corridor.From.Y;
        var dx = Math.Sign(corridor.To.X - x);
        var dy = Math.Sign(corridor.To.Y - y);
        while (x != corridor.To.X)
        {
            yield return new(x, y);
            x += dx;
        }
        while (y != corridor.To.Y)
        {
            yield return new(x, y);
            y += dy;
        }
        yield return corridor.To;
    }

    private static void EnsureObjectiveFerrite(List<AsteroidCellDescriptor> asteroids, int ferriteCells)
    {
        const int minimumFerriteCells = 15;
        for (var index = 0; ferriteCells < minimumFerriteCells && index < asteroids.Count; index++)
        {
            if (asteroids[index].Kind != AsteroidCellKind.Ordinary)
                continue;
            asteroids[index] = asteroids[index] with { Kind = AsteroidCellKind.Ferrite, Health = 45 };
            ferriteCells++;
        }
    }

    private static Pcg32 CreateAttemptRandom(GenerationIdentity identity, int attempt)
    {
        var hash = StableHash.Add(StableHash.Offset, identity.RunSeed);
        hash = StableHash.Add(hash, identity.EnvironmentId.Value);
        hash = StableHash.Add(hash, unchecked((ulong)identity.ContentVersion));
        hash = StableHash.Add(hash, unchecked((ulong)identity.GenerationVersion));
        hash = StableHash.Add(hash, unchecked((ulong)identity.RngVersion));
        hash = StableHash.Add(hash, unchecked((ulong)attempt));
        var streams = new RandomStreams(hash);
        return streams.Get(RngStream.Layout);
    }

    internal static int NextInt(Pcg32 random, int minimumInclusive, int maximumExclusive)
    {
        if (minimumInclusive >= maximumExclusive)
            throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
        var range = (uint)(maximumExclusive - minimumInclusive);
        var threshold = unchecked((uint)(0 - range)) % range;
        uint value;
        do value = random.NextUInt(); while (value < threshold);
        return minimumInclusive + (int)(value % range);
    }

    private static ReadOnlyCollection<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private static void ValidateIdentity(GenerationIdentity identity)
    {
        if (identity.EnvironmentId != WorldRunIds.CinderBelt && identity.EnvironmentId != WorldRunIds.IonVeil)
            throw new ArgumentException("Unsupported environment ID.", nameof(identity));
        if (identity.ContentVersion != ContractVersions.Content ||
            identity.GenerationVersion != CurrentGenerationVersion ||
            identity.RngVersion != ContractVersions.Rng)
            throw new ArgumentException("Unsupported generation identity version.", nameof(identity));
    }
}
