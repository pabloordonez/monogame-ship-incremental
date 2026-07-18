using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record SaveEnvelope(
    DurableVersions Versions,
    string BuildId,
    string CatalogFingerprint,
    ProfileSnapshot Profile,
    string Checksum);
