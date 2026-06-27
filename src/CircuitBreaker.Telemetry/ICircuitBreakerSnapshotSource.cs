namespace CircuitBreaker.Telemetry;

/// <summary>
/// Provides an on-demand <see cref="CircuitBreakerSnapshot"/>.
/// Implement this interface in your circuit breaker to plug it into
/// <see cref="CircuitBreakerTelemetryBackgroundService"/> without introducing
/// a dependency on <c>CircuitBreaker.Adaptive</c> from within the telemetry package.
/// </summary>
public interface ICircuitBreakerSnapshotSource
{
    Task<CircuitBreakerSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
