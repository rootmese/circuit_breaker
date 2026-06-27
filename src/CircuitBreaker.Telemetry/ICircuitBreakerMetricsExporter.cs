namespace CircuitBreaker.Telemetry;

/// <summary>
/// Exports a snapshot to an external system (Prometheus, OpenTelemetry, Zabbix, etc.).
/// </summary>
public interface ICircuitBreakerMetricsExporter
{
    Task ExportAsync(CircuitBreakerSnapshot snapshot, CancellationToken cancellationToken = default);
}
