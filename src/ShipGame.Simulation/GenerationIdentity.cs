using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Simulation;

public readonly record struct GenerationIdentity(
    ContentId EnvironmentId,
    ulong RunSeed,
    int ContentVersion,
    int GenerationVersion,
    int RngVersion)
{
    public static GenerationIdentity Current(ContentId environmentId, ulong runSeed) =>
        new(environmentId, runSeed, ContractVersions.Content, ContractVersions.Generation, ContractVersions.Rng);
}
