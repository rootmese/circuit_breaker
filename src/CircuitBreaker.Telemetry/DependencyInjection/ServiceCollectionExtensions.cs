using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CircuitBreaker.Telemetry.DependencyInjection;

/// <summary>
/// Extension methods for registering circuit breaker telemetry services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="ICircuitBreakerTelemetryPublisher"/> and the hosted
    /// <see cref="CircuitBreakerTelemetryBackgroundService"/> that collects snapshots
    /// at the specified <paramref name="exportInterval"/> and pushes them to all
    /// registered <see cref="ICircuitBreakerMetricsExporter"/> instances.
    /// </summary>
    /// <remarks>
    /// An <see cref="ICircuitBreakerSnapshotSource"/> must be registered separately
    /// (e.g. by calling <c>AddAdaptiveCircuitBreaker</c> which registers
    /// <c>AdaptiveCircuitBreakerDecorator</c> as <see cref="ICircuitBreakerSnapshotSource"/>).
    /// </remarks>
    public static IServiceCollection AddCircuitBreakerTelemetry(
        this IServiceCollection services,
        TimeSpan? exportInterval = null)
    {
        services.TryAddSingleton<ICircuitBreakerTelemetryPublisher>(sp =>
            new CircuitBreakerTelemetryPublisher(
                sp.GetServices<ICircuitBreakerMetricsExporter>(),
                sp.GetService<Microsoft.Extensions.Logging.ILogger<CircuitBreakerTelemetryPublisher>>()));

        services.TryAddSingleton(sp =>
            new CircuitBreakerTelemetryBackgroundService(
                sp.GetRequiredService<ICircuitBreakerSnapshotSource>(),
                sp.GetRequiredService<ICircuitBreakerTelemetryPublisher>(),
                exportInterval,
                sp.GetService<Microsoft.Extensions.Logging.ILogger<CircuitBreakerTelemetryBackgroundService>>()));

        services.AddHostedService(sp =>
            sp.GetRequiredService<CircuitBreakerTelemetryBackgroundService>());

        return services;
    }

    /// <summary>
    /// Registers a concrete <see cref="ICircuitBreakerMetricsExporter"/> implementation.
    /// Multiple exporters can be registered and all will receive each snapshot.
    /// </summary>
    public static IServiceCollection AddCircuitBreakerExporter<T>(
        this IServiceCollection services)
        where T : class, ICircuitBreakerMetricsExporter
    {
        services.AddSingleton<ICircuitBreakerMetricsExporter, T>();
        return services;
    }
}
