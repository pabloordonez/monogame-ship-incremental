using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record TransactionReceiptDto(string TransactionId, string Operation, ulong Fingerprint);
