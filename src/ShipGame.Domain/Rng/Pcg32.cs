using System.Buffers.Binary;
using System.Text;

namespace ShipGame.Domain;

public sealed class Pcg32
{
    private ulong _state;
    private readonly ulong _increment;

    public Pcg32(ulong seed, ulong sequence)
    {
        _increment = (sequence << 1) | 1;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    public ulong State => _state;

    public uint NextUInt()
    {
        var old = _state;
        _state = unchecked(old * 6364136223846793005UL + _increment);
        var xor = (uint)(((old >> 18) ^ old) >> 27);
        var rotation = (int)(old >> 59);
        return (xor >> rotation) | (xor << ((-rotation) & 31));
    }
}
