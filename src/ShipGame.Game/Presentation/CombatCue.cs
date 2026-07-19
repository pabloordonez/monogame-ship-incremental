using System.Numerics;
using Microsoft.Xna.Framework.Input;
using ShipGame.Ecs;
using ShipGame.Gameplay;

namespace ShipGame.Game;

public readonly record struct CombatCue(
    CombatCueKind Kind,
    long Tick,
    EntityId Entity,
    Vector2 Position,
    float Intensity,
    string AssetId);
