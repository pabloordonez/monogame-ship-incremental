using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

/// <summary>Fixed-capacity presentation particle pool. World-space positions.</summary>
internal sealed class ParticleSystem
{
    public const int Capacity = 512;

    private readonly Particle[] _particles = new Particle[Capacity];
    private readonly Random _random = new();
    private int _activeCount;

    public int ActiveCount => _activeCount;

    public void Clear()
    {
        Array.Clear(_particles);
        _activeCount = 0;
    }

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || _activeCount == 0)
            return;

        var remaining = 0;
        for (var i = 0; i < Capacity; i++)
        {
            ref var particle = ref _particles[i];
            if (!particle.Active)
                continue;

            particle.Life -= deltaSeconds;
            if (particle.Life <= 0f)
            {
                particle.Active = false;
                continue;
            }

            particle.Position += particle.Velocity * deltaSeconds;
            particle.Velocity *= MathF.Pow(0.92f, deltaSeconds * 60f);
            remaining++;
        }

        _activeCount = remaining;
    }

    public void Burst(Vector2 worldPosition, in ParticleBurst burst)
    {
        var count = Math.Clamp(burst.Count, 0, Capacity);
        for (var i = 0; i < count; i++)
        {
            if (!TryAllocate(out var index, out var replacedActive))
                break;

            var angle = (float)(_random.NextDouble() * MathF.Tau);
            var speed = Lerp(burst.MinSpeed, burst.MaxSpeed, (float)_random.NextDouble());
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            if (burst.BiasStrength > 0f && burst.BiasDirection != default)
            {
                var bias = Vector2.Normalize(burst.BiasDirection);
                direction = Vector2.Normalize(direction + bias * burst.BiasStrength);
            }

            var life = Lerp(burst.MinLife, burst.MaxLife, (float)_random.NextDouble());
            var t = (float)_random.NextDouble();
            string? regionId = null;
            if (burst.RegionIds is { Length: > 0 })
                regionId = burst.RegionIds[_random.Next(burst.RegionIds.Length)];
            var maxSize = regionId is null ? 3 : 12;
            ref var particle = ref _particles[index];
            particle = new Particle
            {
                Position = worldPosition,
                Velocity = direction * speed,
                Life = life,
                MaxLife = life,
                Color = regionId is null ? LerpColor(burst.ColorA, burst.ColorB, t) : XnaColor.White,
                Size = (byte)Math.Clamp(
                    burst.MinSize + _random.Next(Math.Max(0, burst.MaxSize - burst.MinSize + 1)),
                    1,
                    maxSize),
                Active = true,
                RegionId = regionId
            };
            // Per-particle drag is applied uniformly in Update; burst.Drag reserved for future.
            _ = burst.Drag;
            if (!replacedActive)
                _activeCount++;
        }
    }

    public ReadOnlySpan<Particle> AsSpan() => _particles;

    private bool TryAllocate(out int index, out bool replacedActive)
    {
        for (var i = 0; i < Capacity; i++)
        {
            if (_particles[i].Active)
                continue;
            index = i;
            replacedActive = false;
            return true;
        }

        // Pool full: overwrite the particle closest to expiring.
        var worst = -1;
        var worstLife = float.MaxValue;
        for (var i = 0; i < Capacity; i++)
        {
            ref var candidate = ref _particles[i];
            if (candidate.Life >= worstLife)
                continue;
            worstLife = candidate.Life;
            worst = i;
        }

        index = worst;
        replacedActive = true;
        return index >= 0;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static XnaColor LerpColor(XnaColor a, XnaColor b, float t) =>
        new(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
}
