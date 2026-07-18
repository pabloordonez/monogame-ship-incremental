using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record RunSummaryDto(
    string RunId,
    string EnvironmentId,
    bool Succeeded,
    ResourceAmountsDto Earned,
    ResourceAmountsDto Banked,
    ResourceAmountsDto Retained,
    ResourceAmountsDto Lost);
