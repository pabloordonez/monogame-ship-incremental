using System.Collections.Frozen;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Telemetry;

public sealed class DisabledTelemetrySink : ITelemetrySink
{
    public void Write(TelemetryRecord record) { }
    public void Dispose() { }
}
