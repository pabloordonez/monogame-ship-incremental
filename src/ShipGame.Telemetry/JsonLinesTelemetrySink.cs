using System.Collections.Frozen;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Telemetry;

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
