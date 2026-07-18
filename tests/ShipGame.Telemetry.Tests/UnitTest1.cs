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
        using (ITelemetrySink sink = JsonLinesTelemetrySink.Create(path))
            sink.Write(Telemetry.Event("run.started"));

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, document.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("run.started", document.RootElement.GetProperty("EventName").GetString());
    }

    [Fact]
    public void SinkFailureIsContained()
    {
        var sink = JsonLinesTelemetrySink.Create(Path.Combine(_root, "events.jsonl"));
        sink.Dispose();

        var exception = Record.Exception(() => sink.Write(Telemetry.Event("ignored")));

        Assert.Null(exception);
        Assert.True(sink.Failed);
    }

    [Fact]
    public void PayloadRejectsPiiRawTextCyclesAndUnsupportedObjects()
    {
        Assert.Throws<ArgumentException>(() =>
            Telemetry.Event("test", new Dictionary<string, object?> { ["email"] = 1 }));
        Assert.Throws<ArgumentException>(() =>
            Telemetry.Event("test", new Dictionary<string, object?> { ["detail"] = "raw text" }));

        var cyclic = new Dictionary<string, object?>();
        cyclic["cycle"] = cyclic;
        Assert.Throws<ArgumentException>(() => Telemetry.Event("test", cyclic));
        Assert.Throws<ArgumentException>(() =>
            Telemetry.Event("test", new Dictionary<string, object?> { ["value"] = new Version(1, 0) }));
    }

    [Fact]
    public void PayloadSizeAndFieldCountAreBounded()
    {
        var tooMany = Enumerable.Range(0, Telemetry.MaxPayloadFields + 1)
            .ToDictionary(index => $"field{index}", index => (object?)index);
        Assert.Throws<ArgumentException>(() => Telemetry.Event("test", tooMany));

        var oversized = Enumerable.Range(0, Telemetry.MaxPayloadFields)
            .ToDictionary(
                index => $"field{index:D2}" + new string('x', 52),
                index => (object?)index);
        Assert.Throws<ArgumentException>(() => Telemetry.Event("test", oversized));
    }

    [Fact]
    public void ValidatedPayloadIsFrozenAgainstPostConstructionMutation()
    {
        var source = new Dictionary<string, object?> { ["count"] = 3 };
        var record = Telemetry.Event("run.summary", source);

        Assert.False(record.Payload is IDictionary<string, object?>);
        Assert.False(record.Payload is System.Collections.IDictionary);
        source["email"] = "forbidden@example.test";
        source["count"] = 999;

        var path = Path.Combine(_root, "frozen-events.jsonl");
        using (var sink = JsonLinesTelemetrySink.Create(path))
            sink.Write(record);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var payload = document.RootElement.GetProperty("Payload");
        Assert.Equal(3, payload.GetProperty("count").GetInt32());
        Assert.False(payload.TryGetProperty("email", out _));
        Assert.False(File.ReadAllText(path).Contains("forbidden@example.test", StringComparison.Ordinal));
    }

    [Fact]
    public void SinkConstructionFailureIsContained()
    {
        var exception = Record.Exception(() =>
        {
            using var sink = JsonLinesTelemetrySink.Create("\0");
            Assert.True(sink.Failed);
            sink.Write(Telemetry.Event("ignored"));
        });

        Assert.Null(exception);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
