using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record DurableVersions(
    int Save,
    int Content,
    int Generation,
    int Rng,
    int Replay,
    int Telemetry)
{
    public static DurableVersions Current { get; } = new(
        ContractVersions.Save,
        ContractVersions.Content,
        ContractVersions.Generation,
        ContractVersions.Rng,
        ContractVersions.Replay,
        ContractVersions.Telemetry);
}
