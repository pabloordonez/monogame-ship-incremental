using System.Buffers.Binary;
using System.Text;

namespace ShipGame.Domain;

public enum RngStream
{
    Layout,
    Encounter,
    Ai,
    Loot,
    Upgrade,
    Cosmetic
}
