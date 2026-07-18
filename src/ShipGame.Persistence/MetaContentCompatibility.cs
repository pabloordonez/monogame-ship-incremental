using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record MetaContentCompatibility(
    IReadOnlySet<string> ResearchIds,
    IReadOnlySet<string> EnvironmentIds,
    IReadOnlySet<string> ModuleIds,
    IReadOnlySet<string>? UpgradeIds = null);
