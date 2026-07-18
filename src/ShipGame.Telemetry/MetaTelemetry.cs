using ShipGame.Domain;

namespace ShipGame.Telemetry;

public readonly record struct MetaTelemetryContext(
    ulong InstallId,
    ulong SessionId,
    ulong RunId,
    int BuildCode,
    int ContentCode,
    int GenerationVersion,
    long ElapsedMilliseconds);

public static class MetaTelemetryTranslator
{
    public static TelemetryRecord Translate(MetaTelemetryFact fact, MetaTelemetryContext context)
    {
        if (context.ElapsedMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(context), "Elapsed time cannot be negative.");
        var eventName = fact.Kind switch
        {
            MetaTelemetryFactKind.ScreenEntered => "screen.entered",
            MetaTelemetryFactKind.NewProfile => "profile.new",
            MetaTelemetryFactKind.ContinueProfile => "profile.continued",
            MetaTelemetryFactKind.EnvironmentSelected => "environment.selected",
            MetaTelemetryFactKind.LockInspected => "environment.lock-inspected",
            MetaTelemetryFactKind.RunStarted => "run.started",
            MetaTelemetryFactKind.RunResolved => "run.resolved",
            MetaTelemetryFactKind.ResearchViewed => "research.viewed",
            MetaTelemetryFactKind.ResearchPurchased => "research.purchased",
            MetaTelemetryFactKind.ResearchRejected => "research.rejected",
            MetaTelemetryFactKind.LoadoutChanged => "loadout.changed",
            MetaTelemetryFactKind.SaveStarted => "save.started",
            MetaTelemetryFactKind.SaveSucceeded => "save.succeeded",
            MetaTelemetryFactKind.SaveFailed => "save.failed",
            MetaTelemetryFactKind.SaveRecovered => "save.recovered",
            MetaTelemetryFactKind.OptionChanged => "option.changed",
            _ => throw new ArgumentOutOfRangeException(nameof(fact))
        };
        return Telemetry.Event(eventName, new Dictionary<string, object?>
        {
            ["installId"] = context.InstallId,
            ["sessionId"] = context.SessionId,
            ["runId"] = context.RunId,
            ["buildCode"] = context.BuildCode,
            ["contentCode"] = context.ContentCode,
            ["generationVersion"] = context.GenerationVersion,
            ["elapsedMilliseconds"] = context.ElapsedMilliseconds,
            ["subjectCode"] = fact.SubjectCode,
            ["amount"] = fact.Amount,
            ["succeeded"] = fact.Succeeded
        });
    }
}

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
