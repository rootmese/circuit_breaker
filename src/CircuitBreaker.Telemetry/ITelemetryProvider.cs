namespace CircuitBreaker.Telemetry;

/// <summary>
/// Supplies aggregated telemetry for adaptive control decisions.
/// </summary>
public interface ITelemetryProvider
{
    Task<TelemetrySnapshot> CollectAsync(CancellationToken cancellationToken = default);
}
