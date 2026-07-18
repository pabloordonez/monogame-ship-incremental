using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed class SaveMigrationRegistry
{
    public IReadOnlyList<int> SupportedSaveVersions { get; } = [ContractVersions.Save];

    public CompatibilityStatus Classify(
        DurableVersions versions,
        string catalogFingerprint,
        string expectedCatalogFingerprint)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Save != ContractVersions.Save)
            return versions.Save > ContractVersions.Save
                ? CompatibilityStatus.IncompatibleNewer
                : CompatibilityStatus.Corrupt;
        if (versions.Content != ContractVersions.Content ||
            !string.Equals(catalogFingerprint, expectedCatalogFingerprint, StringComparison.Ordinal))
            return CompatibilityStatus.MissingContent;

        var remaining = new[]
        {
            (versions.Generation, ContractVersions.Generation),
            (versions.Rng, ContractVersions.Rng),
            (versions.Replay, ContractVersions.Replay),
            (versions.Telemetry, ContractVersions.Telemetry)
        };
        if (remaining.Any(pair => pair.Item1 > pair.Item2))
            return CompatibilityStatus.IncompatibleNewer;
        if (remaining.Any(pair => pair.Item1 < pair.Item2))
            return CompatibilityStatus.Corrupt;
        return CompatibilityStatus.Supported;
    }
}
