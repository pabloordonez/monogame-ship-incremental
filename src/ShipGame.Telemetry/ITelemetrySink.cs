using System.Collections.Frozen;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Telemetry;

public interface ITelemetrySink : IDisposable
{
    void Write(TelemetryRecord record);
}
