using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Telemetry;

public sealed record TelemetryRecord(
    int SchemaVersion,
    string EventName,
    DateTimeOffset UtcTimestamp,
    IReadOnlyDictionary<string, object?> Payload);

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
    private readonly StreamWriter _writer;
    private bool _failed;

    public JsonLinesTelemetrySink(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _writer = new StreamWriter(new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read));
    }

    public bool Failed => _failed;

    public void Write(TelemetryRecord record)
    {
        if (_failed)
            return;
        try
        {
            _writer.WriteLine(JsonSerializer.Serialize(record));
            _writer.Flush();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            _failed = true;
        }
    }

    public void Dispose() => _writer.Dispose();
}

public static class Telemetry
{
    public static TelemetryRecord Event(string name, IReadOnlyDictionary<string, object?>? payload = null) =>
        new(ContractVersions.Telemetry, name, DateTimeOffset.UtcNow, payload ?? new Dictionary<string, object?>());
}
