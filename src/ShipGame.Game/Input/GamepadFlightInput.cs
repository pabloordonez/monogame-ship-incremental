using System.Numerics;
using Microsoft.Xna.Framework.Input;
using ShipGame.Ecs;
using ShipGame.Gameplay;

namespace ShipGame.Game;

public readonly record struct GamepadFlightInput(
    Vector2 Move,
    Vector2 Aim,
    bool Fire,
    bool Mine,
    bool Mobility,
    bool Interact);
