using System.Collections.Frozen;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Telemetry;

public sealed class TelemetryRecord
{
    internal TelemetryRecord(
        int schemaVersion,
        string eventName,
        DateTimeOffset utcTimestamp,
        IReadOnlyDictionary<string, object?> payload)
    {
        SchemaVersion = schemaVersion;
        EventName = eventName;
        UtcTimestamp = utcTimestamp;
        Payload = new FrozenPayload(payload);
    }

    public int SchemaVersion { get; }
    public string EventName { get; }
    public DateTimeOffset UtcTimestamp { get; }
    public IReadOnlyDictionary<string, object?> Payload { get; }

    private sealed class FrozenPayload : IReadOnlyDictionary<string, object?>
    {
        private readonly FrozenDictionary<string, object?> _values;

        public FrozenPayload(IReadOnlyDictionary<string, object?> values)
        {
            _values = values.ToFrozenDictionary(StringComparer.Ordinal);
        }

        public object? this[string key] => _values[key];
        public IEnumerable<string> Keys => _values.Keys;
        public IEnumerable<object?> Values => _values.Values;
        public int Count => _values.Count;
        public bool ContainsKey(string key) => _values.ContainsKey(key);
        public bool TryGetValue(string key, out object? value) => _values.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

public interface ITelemetrySink : IDisposable
{
    void Write(TelemetryRecord record);
}

public sealed class DisabledTelemetrySink : ITelemetrySink
{
    public void Write(TelemetryRecord record) { }
    public void Dispose() { }
}

public sealed class JsonLinesTelemetrySink : ITelemetrySink
{
    private StreamWriter? _writer;
    private bool _failed;

    private JsonLinesTelemetrySink() { }

    public static JsonLinesTelemetrySink Create(string path)
    {
        var sink = new JsonLinesTelemetrySink();
        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new IOException("Telemetry path has no directory.");
            Directory.CreateDirectory(directory);
            sink._writer = new StreamWriter(new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read));
        }
        catch (Exception exception) when (IsContainable(exception))
        {
            sink._failed = true;
        }
        return sink;
    }

    public bool Failed => _failed;

    public void Write(TelemetryRecord record)
    {
        if (_failed)
            return;
        try
        {
            ArgumentNullException.ThrowIfNull(record);
            _writer!.WriteLine(JsonSerializer.Serialize(record));
            _writer.Flush();
        }
        catch (Exception exception) when (IsContainable(exception))
        {
            _failed = true;
        }
    }

    public void Dispose()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (Exception exception) when (IsContainable(exception))
        {
            _failed = true;
        }
    }

    private static bool IsContainable(Exception exception) =>
        exception is not OutOfMemoryException and not AccessViolationException;
}

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
