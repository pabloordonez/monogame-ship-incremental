using System.Collections.Frozen;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Telemetry;

public static class Telemetry
{
    public const int MaxPayloadFields = 24;
    public const int MaxPayloadBytes = 2048;
    private static readonly string[] ProhibitedFieldFragments =
        ["name", "email", "username", "address", "ip", "hardware", "raw", "text", "snapshot"];

    public static TelemetryRecord Event(string name, IReadOnlyDictionary<string, object?>? payload = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64 ||
            name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
            throw new ArgumentException("Telemetry event names must be canonical and at most 64 characters.", nameof(name));

        payload ??= new Dictionary<string, object?>();
        if (payload.Count > MaxPayloadFields)
            throw new ArgumentException($"Telemetry payloads are limited to {MaxPayloadFields} fields.", nameof(payload));

        var sanitized = new Dictionary<string, object?>(payload.Count, StringComparer.Ordinal);
        var estimatedBytes = 0;
        foreach (var (key, value) in payload)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 64 ||
                key.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
                throw new ArgumentException("Telemetry field names must be canonical and at most 64 characters.", nameof(payload));
            if (ProhibitedFieldFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Telemetry field '{key}' is prohibited by the no-PII/raw-data policy.", nameof(payload));
            if (!IsSupportedScalar(value))
                throw new ArgumentException($"Telemetry field '{key}' has an unsupported value type.", nameof(payload));
            estimatedBytes += key.Length * 2 + 40;
            sanitized.Add(key, value);
        }
        if (estimatedBytes > MaxPayloadBytes)
            throw new ArgumentException($"Telemetry payload exceeds {MaxPayloadBytes} bytes.", nameof(payload));

        return new TelemetryRecord(
            ContractVersions.Telemetry,
            name,
            DateTimeOffset.UtcNow,
            sanitized);
    }

    private static bool IsSupportedScalar(object? value) => value switch
    {
        null or bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal => true,
        float number => float.IsFinite(number),
        double number => double.IsFinite(number),
        _ => false
    };
}
