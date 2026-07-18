using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record MetaSaveEnvelope(
    MetaSaveVersions Versions,
    string BuildId,
    string CatalogFingerprint,
    MetaProfileDto Profile,
    string Checksum);
