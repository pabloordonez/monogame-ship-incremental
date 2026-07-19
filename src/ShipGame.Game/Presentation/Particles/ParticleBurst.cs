using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ShipGame.Game;

internal readonly record struct ParticleBurst(
    int Count,
    float MinSpeed,
    float MaxSpeed,
    float MinLife,
    float MaxLife,
    XnaColor ColorA,
    XnaColor ColorB,
    byte MinSize,
    byte MaxSize,
    float Drag = 0.92f,
    Vector2 BiasDirection = default,
    float BiasStrength = 0f);
