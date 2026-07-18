using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record MetaSaveVersions(
    int Save,
    int Content,
    int Generation,
    int Rng,
    int Replay,
    int Telemetry)
{
    public static MetaSaveVersions Current { get; } = new(
        MetaSaveSchema.Current,
        ContractVersions.Content,
        ContractVersions.Generation,
        ContractVersions.Rng,
        ContractVersions.Replay,
        ContractVersions.Telemetry);
}
