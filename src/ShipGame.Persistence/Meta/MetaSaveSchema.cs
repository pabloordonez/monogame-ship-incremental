using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public static class MetaSaveSchema
{
    /// <summary>P0 foundation profile.json schema.</summary>
    public const int Foundation = 1;

    /// <summary>
    /// P4 meta profile-v2.json schema. Increment when persisted meta shape/meaning changes.
    /// Foundation <see cref="ContractVersions.Save"/> remains 1 for the walking-skeleton file.
    /// </summary>
    public const int Current = 2;

    public const int MaxFileBytes = 4 * 1024 * 1024;
    public const int MaxStringLength = 128;
    public const int MaxCollectionCount = 4096;
}
