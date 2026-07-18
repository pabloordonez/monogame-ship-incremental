using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record LifetimeCountersDto(
    long Extractions,
    long NormalKills,
    long EliteKills,
    long FerriteCollected,
    long ResourceCellsBroken,
    long IonVeilExtractions);
