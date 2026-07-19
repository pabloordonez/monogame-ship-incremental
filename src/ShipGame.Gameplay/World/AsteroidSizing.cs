namespace ShipGame.Gameplay;

public static class AsteroidSizing
{
    public static float Radius(AsteroidCellSize size) => size switch
    {
        AsteroidCellSize.Small => 8f,
        AsteroidCellSize.Large => 18f,
        _ => 12f
    };

    public static int DrawSize(AsteroidCellSize size) => size switch
    {
        AsteroidCellSize.Small => 16,
        AsteroidCellSize.Large => 36,
        _ => 24
    };

    public static int MaxHealth(AsteroidCellKind kind, AsteroidCellSize size) => (kind, size) switch
    {
        (AsteroidCellKind.Ordinary, AsteroidCellSize.Small) => 35,
        (AsteroidCellKind.Ordinary, AsteroidCellSize.Large) => 90,
        (AsteroidCellKind.Ordinary, _) => 60,
        (_, AsteroidCellSize.Small) => 28,
        (_, AsteroidCellSize.Large) => 70,
        _ => 45
    };

    public static string AtlasRegion(AsteroidCellKind kind, AsteroidCellSize size)
    {
        var sizeKey = size switch
        {
            AsteroidCellSize.Small => "small",
            AsteroidCellSize.Large => "large",
            _ => "medium"
        };
        var kindKey = kind switch
        {
            AsteroidCellKind.Ferrite => "ferrite",
            AsteroidCellKind.Lumen => "lumen",
            _ => "ordinary"
        };
        return $"asteroids/{sizeKey}/{kindKey}";
    }
}
