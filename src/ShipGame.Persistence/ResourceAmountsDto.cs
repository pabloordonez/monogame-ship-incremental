using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record ResourceAmountsDto(long Ferrite, long Lumen, long DataCores);
