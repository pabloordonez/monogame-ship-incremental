using System.Numerics;
using Microsoft.Xna.Framework.Input;
using ShipGame.Ecs;
using ShipGame.Gameplay;

namespace ShipGame.Game;

public enum CombatCueKind : byte
{
    Weapon,
    Impact,
    Shield,
    ShieldBreak,
    Hull,
    Destruction,
    Mobility,
    Rejected,
    Spawn,
    Telegraph
}
