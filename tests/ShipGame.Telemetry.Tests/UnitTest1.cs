using System.Text.Json;

namespace ShipGame.Telemetry.Tests;

public class TelemetryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ShipGame-Telemetry-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void DisabledSinkWritesNothing()
    {
        using ITelemetrySink sink = new DisabledTelemetrySink();
        sink.Write(Telemetry.Event("session.started"));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void LocalSinkWritesVersionedJsonLines()
    {
        var path = Path.Combine(_root, "events.jsonl");
        using (ITelemetrySink sink = new JsonLinesTelemetrySink(path))
            sink.Write(Telemetry.Event("run.started"));

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, document.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("run.started", document.RootElement.GetProperty("EventName").GetString());
    }

    [Fact]
    public void SinkFailureIsContained()
    {
        var sink = new JsonLinesTelemetrySink(Path.Combine(_root, "events.jsonl"));
        sink.Dispose();

        var exception = Record.Exception(() => sink.Write(Telemetry.Event("ignored")));

        Assert.Null(exception);
        Assert.True(sink.Failed);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
