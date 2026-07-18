using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public enum CompatibilityStatus
{
    Supported,
    Migratable,
    IncompatibleNewer,
    Corrupt,
    Missing,
    MissingContent
}
