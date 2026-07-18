using ShipGame.Domain;

namespace ShipGame.Telemetry;

public sealed class ConsentAwareTelemetry : IDisposable
{
    private readonly Func<ITelemetrySink> _sinkFactory;
    private ITelemetrySink? _sink;
    private bool _consent;
    private bool _failed;

    public ConsentAwareTelemetry(bool consent, Func<ITelemetrySink> sinkFactory)
    {
        _consent = consent;
        _sinkFactory = sinkFactory ?? throw new ArgumentNullException(nameof(sinkFactory));
    }

    public bool Consent => _consent;
    public bool Failed => _failed;

    public void SetConsent(bool consent)
    {
        if (_consent == consent)
            return;
        _consent = consent;
        if (!consent)
            DisposeSink();
    }

    public void Record(MetaTelemetryFact fact, MetaTelemetryContext context)
    {
        if (!_consent || _failed)
            return;
        try
        {
            _sink ??= _sinkFactory();
            _sink.Write(MetaTelemetryTranslator.Translate(fact, context));
        }
        catch (Exception exception) when (IsContainable(exception))
        {
            _failed = true;
            DisposeSink();
        }
    }

    public void Dispose() => DisposeSink();

    private void DisposeSink()
    {
        try
        {
            _sink?.Dispose();
        }
        catch (Exception exception) when (IsContainable(exception))
        {
            _failed = true;
        }
        finally
        {
            _sink = null;
        }
    }

    private static bool IsContainable(Exception exception) =>
        exception is not OutOfMemoryException and not AccessViolationException;
}
