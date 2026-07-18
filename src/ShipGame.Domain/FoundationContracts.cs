using System.Buffers.Binary;
using System.Text;

namespace ShipGame.Domain;

public static class ContractVersions
{
    public const int Save = 1;
    public const int Content = 1;
    public const int Generation = 1;
    public const int Rng = 1;
    public const int Replay = 1;
    public const int Telemetry = 1;
}

public readonly record struct ContentId
{
    public string Value { get; }

    public ContentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            throw new ArgumentException("Content IDs must contain 1-128 characters.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
}

public enum RngStream
{
    Layout,
    Encounter,
    Ai,
    Loot,
    Upgrade,
    Cosmetic
}

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

public sealed class RandomStreams
{
    private readonly Dictionary<RngStream, Pcg32> _streams;

    public RandomStreams(ulong runSeed)
    {
        _streams = Enum.GetValues<RngStream>().ToDictionary(
            stream => stream,
            stream =>
            {
                var nameHash = StableHash.Add(StableHash.Offset, stream.ToString());
                return new Pcg32(StableHash.Add(nameHash, runSeed), nameHash);
            });
    }

    public Pcg32 Get(RngStream stream) => _streams[stream];

    public ulong CalculateStateHash()
    {
        var hash = StableHash.Offset;
        foreach (var stream in Enum.GetValues<RngStream>())
        {
            hash = StableHash.Add(hash, (ulong)stream);
            hash = StableHash.Add(hash, _streams[stream].State);
        }
        return hash;
    }
}

public readonly record struct ProfileSnapshot(ulong ProfileSeed, long RunIndex);
