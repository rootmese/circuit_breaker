using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CircuitBreaker.Telemetry;

/// <summary>
/// Publishes a <see cref="CircuitBreakerSnapshot"/> to all registered
/// <see cref="ICircuitBreakerMetricsExporter"/> instances in parallel.
/// A failure in one exporter does not affect the others.
/// </summary>
public sealed class CircuitBreakerTelemetryPublisher : ICircuitBreakerTelemetryPublisher
{
    private readonly IEnumerable<ICircuitBreakerMetricsExporter> _exporters;
    private readonly ILogger _logger;

    public CircuitBreakerTelemetryPublisher(
        IEnumerable<ICircuitBreakerMetricsExporter> exporters,
        ILogger<CircuitBreakerTelemetryPublisher>? logger = null)
    {
        _exporters = exporters ?? Enumerable.Empty<ICircuitBreakerMetricsExporter>();
        _logger = logger ?? NullLogger<CircuitBreakerTelemetryPublisher>.Instance;
    }

    public async Task PublishAsync(CircuitBreakerSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (!_exporters.Any()) return;

        var tasks = _exporters.Select(exporter =>
            ExportWithCatchAsync(exporter, snapshot, cancellationToken));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task ExportWithCatchAsync(
        ICircuitBreakerMetricsExporter exporter,
        CircuitBreakerSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            await exporter.ExportAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exporter {ExporterType} failed", exporter.GetType().Name);
        }
    }
}
