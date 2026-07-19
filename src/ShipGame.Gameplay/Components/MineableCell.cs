using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public struct MineableCell
{
    public int CellId;
    public AsteroidCellKind Kind;
    public int Health;
    public bool Broken;
}
