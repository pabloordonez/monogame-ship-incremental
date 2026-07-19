using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal struct Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Life;
    public float MaxLife;
    public XnaColor Color;
    public byte Size;
    public bool Active;
    /// <summary>When set, draw this atlas region instead of a colored Fill square.</summary>
    public string? RegionId;
}
