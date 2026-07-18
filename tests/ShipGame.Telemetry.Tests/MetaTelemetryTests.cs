using ShipGame.Domain;

namespace ShipGame.Telemetry.Tests;

public sealed class MetaTelemetryTests
{
    [Fact]
    public void TranslatorEmitsVersionedCanonicalEvents()
    {
        var record = MetaTelemetryTranslator.Translate(
            new MetaTelemetryFact(MetaTelemetryFactKind.ResearchPurchased, 11, 25, true),
            new MetaTelemetryContext(1, 2, 3, 4, 1, 1, 50));

        Assert.Equal(ContractVersions.Telemetry, record.SchemaVersion);
        Assert.Equal("research.purchased", record.EventName);
        Assert.Equal(25L, record.Payload["amount"]);
        Assert.Equal(11, record.Payload["subjectCode"]);
        Assert.DoesNotContain(record.Payload.Keys, key =>
            key.Contains("email", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("name", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("raw", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConsentDisablePreventsWrites()
    {
        var writes = 0;
        using var telemetry = new ConsentAwareTelemetry(false, () => new CountingSink(() => writes++));
        telemetry.Record(new MetaTelemetryFact(MetaTelemetryFactKind.NewProfile), Context());

        Assert.Equal(0, writes);
        Assert.False(telemetry.Consent);
    }

    [Fact]
    public void SinkFailureNeverThrowsAndDisablesFurtherWrites()
    {
        var writes = 0;
        using var telemetry = new ConsentAwareTelemetry(true, () => new FailingSink(() => writes++));

        var exception = Record.Exception(() =>
            telemetry.Record(new MetaTelemetryFact(MetaTelemetryFactKind.SaveStarted), Context()));
        var second = Record.Exception(() =>
            telemetry.Record(new MetaTelemetryFact(MetaTelemetryFactKind.SaveSucceeded), Context()));

        Assert.Null(exception);
        Assert.Null(second);
        Assert.True(telemetry.Failed);
        Assert.Equal(1, writes);
    }

    [Fact]
    public void RevokingConsentDisposesSinkAndStopsEmission()
    {
        var writes = 0;
        var disposed = 0;
        using var telemetry = new ConsentAwareTelemetry(true, () => new CountingSink(() => writes++, () => disposed++));
        telemetry.Record(new MetaTelemetryFact(MetaTelemetryFactKind.OptionChanged), Context());
        telemetry.SetConsent(false);
        telemetry.Record(new MetaTelemetryFact(MetaTelemetryFactKind.OptionChanged), Context());

        Assert.Equal(1, writes);
        Assert.Equal(1, disposed);
        Assert.False(telemetry.Consent);
    }

    private static MetaTelemetryContext Context() => new(1, 2, 3, 4, 1, 1, 0);

    private sealed class CountingSink : ITelemetrySink
    {
        private readonly Action _onWrite;
        private readonly Action? _onDispose;

        public CountingSink(Action onWrite, Action? onDispose = null)
        {
            _onWrite = onWrite;
            _onDispose = onDispose;
        }

        public void Write(TelemetryRecord record) => _onWrite();
        public void Dispose() => _onDispose?.Invoke();
    }

    private sealed class FailingSink : ITelemetrySink
    {
        private readonly Action _onWrite;

        public FailingSink(Action onWrite) => _onWrite = onWrite;

        public void Write(TelemetryRecord record)
        {
            _onWrite();
            throw new IOException("sink unavailable");
        }

        public void Dispose() { }
    }
}
