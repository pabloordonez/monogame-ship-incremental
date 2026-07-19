using ShipGame.Gameplay;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal static class ParticlePresets
{
    public static ParticleBurst MiningSparks { get; } = new(
        Count: 4,
        MinSpeed: 18f,
        MaxSpeed: 55f,
        MinLife: 0.08f,
        MaxLife: 0.22f,
        ColorA: new XnaColor(140, 230, 255),
        ColorB: new XnaColor(220, 250, 255),
        MinSize: 1,
        MaxSize: 2,
        Drag: 0.88f);

    public static ParticleBurst Impact { get; } = new(
        Count: 8,
        MinSpeed: 30f,
        MaxSpeed: 90f,
        MinLife: 0.10f,
        MaxLife: 0.28f,
        ColorA: new XnaColor(255, 200, 90),
        ColorB: new XnaColor(255, 240, 180),
        MinSize: 1,
        MaxSize: 2,
        Drag: 0.90f);

    public static ParticleBurst ShieldHit { get; } = new(
        Count: 10,
        MinSpeed: 25f,
        MaxSpeed: 80f,
        MinLife: 0.12f,
        MaxLife: 0.30f,
        ColorA: new XnaColor(70, 170, 230),
        ColorB: new XnaColor(160, 230, 255),
        MinSize: 1,
        MaxSize: 2,
        Drag: 0.90f);

    public static ParticleBurst ShieldBreak { get; } = new(
        Count: 16,
        MinSpeed: 40f,
        MaxSpeed: 110f,
        MinLife: 0.15f,
        MaxLife: 0.40f,
        ColorA: new XnaColor(60, 150, 220),
        ColorB: new XnaColor(200, 240, 255),
        MinSize: 1,
        MaxSize: 3,
        Drag: 0.88f);

    public static ParticleBurst HullHit { get; } = new(
        Count: 10,
        MinSpeed: 35f,
        MaxSpeed: 100f,
        MinLife: 0.12f,
        MaxLife: 0.32f,
        ColorA: new XnaColor(220, 70, 60),
        ColorB: new XnaColor(255, 160, 80),
        MinSize: 1,
        MaxSize: 2,
        Drag: 0.90f);

    public static ParticleBurst Destruction { get; } = new(
        Count: 36,
        MinSpeed: 40f,
        MaxSpeed: 160f,
        MinLife: 0.25f,
        MaxLife: 0.70f,
        ColorA: new XnaColor(255, 120, 40),
        ColorB: new XnaColor(255, 230, 140),
        MinSize: 1,
        MaxSize: 3,
        Drag: 0.94f);

    public static ParticleBurst AsteroidBreak(AsteroidCellKind kind) => kind switch
    {
        AsteroidCellKind.Ferrite => new(
            Count: 16,
            MinSpeed: 28f,
            MaxSpeed: 110f,
            MinLife: 0.22f,
            MaxLife: 0.55f,
            ColorA: XnaColor.White,
            ColorB: XnaColor.White,
            MinSize: 6,
            MaxSize: 10,
            Drag: 0.92f,
            RegionIds:
            [
                "asteroids/debris/ferrite-a",
                "asteroids/debris/ferrite-b",
                "asteroids/debris/rock-a",
                "asteroids/debris/rock-b"
            ]),
        AsteroidCellKind.Lumen => new(
            Count: 16,
            MinSpeed: 30f,
            MaxSpeed: 115f,
            MinLife: 0.22f,
            MaxLife: 0.58f,
            ColorA: XnaColor.White,
            ColorB: XnaColor.White,
            MinSize: 6,
            MaxSize: 10,
            Drag: 0.92f,
            RegionIds:
            [
                "asteroids/debris/lumen-a",
                "asteroids/debris/lumen-b",
                "asteroids/debris/rock-a",
                "asteroids/debris/rock-b"
            ]),
        _ => new(
            Count: 12,
            MinSpeed: 22f,
            MaxSpeed: 95f,
            MinLife: 0.18f,
            MaxLife: 0.48f,
            ColorA: XnaColor.White,
            ColorB: XnaColor.White,
            MinSize: 6,
            MaxSize: 10,
            Drag: 0.92f,
            RegionIds: ["asteroids/debris/rock-a", "asteroids/debris/rock-b"])
    };

    public static ParticleBurst BeamTip { get; } = new(
        Count: 3,
        MinSpeed: 20f,
        MaxSpeed: 60f,
        MinLife: 0.06f,
        MaxLife: 0.18f,
        ColorA: new XnaColor(255, 180, 70),
        ColorB: new XnaColor(255, 240, 180),
        MinSize: 1,
        MaxSize: 2,
        Drag: 0.85f);

    public static ParticleBurst MissileSmoke { get; } = new(
        Count: 2,
        MinSpeed: 4f,
        MaxSpeed: 18f,
        MinLife: 0.18f,
        MaxLife: 0.45f,
        ColorA: new XnaColor(120, 130, 140),
        ColorB: new XnaColor(180, 190, 200),
        MinSize: 1,
        MaxSize: 2,
        Drag: 0.92f);

    public static ParticleBurst MissileLaunch { get; } = new(
        Count: 6,
        MinSpeed: 20f,
        MaxSpeed: 55f,
        MinLife: 0.10f,
        MaxLife: 0.28f,
        ColorA: new XnaColor(140, 150, 160),
        ColorB: new XnaColor(220, 230, 240),
        MinSize: 1,
        MaxSize: 3,
        Drag: 0.88f);

    public static ParticleBurst ThrustFlame(System.Numerics.Vector2 aftDirection) => new(
        Count: 3,
        MinSpeed: 40f,
        MaxSpeed: 110f,
        MinLife: 0.08f,
        MaxLife: 0.22f,
        ColorA: new XnaColor(255, 140, 40),
        ColorB: new XnaColor(255, 230, 140),
        MinSize: 1,
        MaxSize: 3,
        Drag: 0.86f,
        BiasDirection: aftDirection,
        BiasStrength: 2.4f);

    public static ParticleBurst SeismicBlast { get; } = new(
        Count: 28,
        MinSpeed: 40f,
        MaxSpeed: 160f,
        MinLife: 0.18f,
        MaxLife: 0.45f,
        ColorA: new XnaColor(255, 140, 40),
        ColorB: new XnaColor(255, 230, 140),
        MinSize: 1,
        MaxSize: 3,
        Drag: 0.90f);

    public static ParticleBurst ThrustEmber(System.Numerics.Vector2 aftDirection) => new(
        Count: 2,
        MinSpeed: 20f,
        MaxSpeed: 60f,
        MinLife: 0.12f,
        MaxLife: 0.30f,
        ColorA: new XnaColor(80, 160, 255),
        ColorB: new XnaColor(180, 230, 255),
        MinSize: 1,
        MaxSize: 2,
        Drag: 0.90f,
        BiasDirection: aftDirection,
        BiasStrength: 1.8f);
}
