using System.Buffers.Binary;
using System.Text;

namespace ShipGame.Domain;

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
