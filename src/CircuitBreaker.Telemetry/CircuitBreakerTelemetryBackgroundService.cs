using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CircuitBreaker.Telemetry;

/// <summary>
/// Hosted background service that periodically collects a
/// <see cref="CircuitBreakerSnapshot"/> from an <see cref="ICircuitBreakerSnapshotSource"/>
/// and forwards it to the <see cref="ICircuitBreakerTelemetryPublisher"/>.
/// </summary>
public sealed class CircuitBreakerTelemetryBackgroundService : BackgroundService
{
    private readonly ICircuitBreakerSnapshotSource _source;
    private readonly ICircuitBreakerTelemetryPublisher _publisher;
    private readonly TimeSpan _interval;
    private readonly ILogger _logger;

    public CircuitBreakerTelemetryBackgroundService(
        ICircuitBreakerSnapshotSource source,
        ICircuitBreakerTelemetryPublisher publisher,
        TimeSpan? interval = null,
        ILogger<CircuitBreakerTelemetryBackgroundService>? logger = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _interval = interval ?? TimeSpan.FromSeconds(5);
        _logger = logger ?? NullLogger<CircuitBreakerTelemetryBackgroundService>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Telemetry export started (interval {Interval}s)",
            _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _source.GetSnapshotAsync(stoppingToken).ConfigureAwait(false);
                await _publisher.PublishAsync(snapshot, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error collecting or publishing telemetry snapshot");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Telemetry export stopped");
    }
}
