namespace CircuitBreaker.Telemetry;

/// <summary>
/// Publishes a <see cref="CircuitBreakerSnapshot"/> to all registered exporters.
/// </summary>
public interface ICircuitBreakerTelemetryPublisher
{
    Task PublishAsync(CircuitBreakerSnapshot snapshot, CancellationToken cancellationToken = default);
}
