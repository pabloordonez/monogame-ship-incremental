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
