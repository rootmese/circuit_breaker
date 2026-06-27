using CircuitBreaker.Core;

namespace CircuitBreaker.Telemetry;

/// <summary>
/// Immutable snapshot of the circuit breaker's current state and metrics.
/// </summary>
public sealed record CircuitBreakerSnapshot(
    string ResourceName,
    Guid InstanceId,
    CircuitState State,
    double ErrorRate,
    double LatencyMs,
    double P99LatencyMs,
    double Throughput,
    double TimeoutRate,
    double ResourceSaturation,
    int ActiveConnections,
    double HealthScore,
    DateTimeOffset Timestamp
);
