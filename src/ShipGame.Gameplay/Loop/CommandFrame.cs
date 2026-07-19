using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct CommandFrame(
    long TargetTick,
    short MoveX = 0,
    short MoveY = 0,
    short AimX = 0,
    short AimY = 0,
    bool Confirm = false,
    bool Return = false)
{
    public static CommandFrame Neutral(long tick) => new(tick);
}
