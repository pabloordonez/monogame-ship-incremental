using System.Buffers.Binary;
using System.Text;

namespace ShipGame.Domain;

public static class StableHash
{
    public const ulong Offset = 14695981039346656037UL;
    public const ulong Prime = 1099511628211UL;

    public static ulong Add(ulong hash, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            hash ^= value;
            hash *= Prime;
        }
        return hash;
    }

    public static ulong Add(ulong hash, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return Add(hash, bytes);
    }

    public static ulong Add(ulong hash, string value) => Add(hash, Encoding.UTF8.GetBytes(value));
}
