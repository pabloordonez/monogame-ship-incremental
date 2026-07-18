using System.Numerics;
using Microsoft.Xna.Framework.Input;
using ShipGame.Ecs;
using ShipGame.Simulation;

namespace ShipGame.Game;

public readonly record struct KeyboardFlightInput(
    bool Up,
    bool Down,
    bool Left,
    bool Right,
    Vector2 Aim,
    bool Fire,
    bool Mine,
    bool Mobility,
    bool Interact);
