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
