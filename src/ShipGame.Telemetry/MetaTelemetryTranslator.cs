using ShipGame.Domain;

namespace ShipGame.Telemetry;

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
