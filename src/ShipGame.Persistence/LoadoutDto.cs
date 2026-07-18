using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record LoadoutDto(
    string Weapon,
    string Mining,
    string Shield,
    string Engine,
    string Utility);
