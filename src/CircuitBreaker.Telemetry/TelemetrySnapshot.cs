namespace CircuitBreaker.Telemetry;

/// <summary>
/// Point-in-time metrics used to compute service health.
/// </summary>
public readonly struct TelemetrySnapshot
{
    public double ErrorRate { get; init; }
    public double Throughput { get; init; }
    public double LatencyMs { get; init; }
    public double P99LatencyMs { get; init; }
    public double TimeoutRate { get; init; }
    public double ResourceSaturation { get; init; }
    public int ActiveConnections { get; init; }
    public DateTime Timestamp { get; init; }

    public override string ToString() =>
        $"Error:{ErrorRate:P1}, Latency:{LatencyMs:F0}ms, P99:{P99LatencyMs:F0}ms, " +
        $"Throughput:{Throughput:F0}/s, Timeouts:{TimeoutRate:P1}, Saturation:{ResourceSaturation:P0}";
}
