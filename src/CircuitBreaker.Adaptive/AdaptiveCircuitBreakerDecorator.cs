using CircuitBreaker.Core;
using CircuitBreaker.Telemetry;
using Microsoft.Extensions.Logging;

namespace CircuitBreaker.Adaptive;

/// <summary>
/// Wraps <see cref="ICircuitBreaker"/> with adaptive rate/concurrency limits and telemetry.
/// </summary>
public sealed class AdaptiveCircuitBreakerDecorator : ICircuitBreaker, IAsyncDisposable
{
    private readonly ICircuitBreaker _inner;
    private readonly AdaptiveTrafficController _controller;
    private readonly AdaptiveRateLimiter _rateLimiter;
    private readonly AdaptiveConcurrencyLimiter _concurrencyLimiter;
    private readonly IExecutionTelemetryRecorder _telemetry;

    public CircuitState State => _inner.State;

    public string ResourceName => _inner.ResourceName;

    public HealthScore CurrentHealthScore => _controller.CurrentHealthScore;

    private AdaptiveCircuitBreakerDecorator(
        ICircuitBreaker inner,
        RollingWindowTelemetryProvider telemetry,
        AdaptiveRateLimiter rateLimiter,
        AdaptiveConcurrencyLimiter concurrencyLimiter,
        AdaptiveTrafficController controller)
    {
        _inner = inner;
        _telemetry = telemetry;
        _rateLimiter = rateLimiter;
        _concurrencyLimiter = concurrencyLimiter;
        _controller = controller;
    }

    public static AdaptiveCircuitBreakerDecorator Create(
        ICircuitBreaker inner,
        AdaptiveTrafficControlOptions? options = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);

        var opts = options ?? new AdaptiveTrafficControlOptions();
        var telemetry = new RollingWindowTelemetryProvider(opts.TelemetryWindow);
        var rateLimiter = new AdaptiveRateLimiter(opts.InitialMaxRequestsPerSecond, logger);
        var concurrencyLimiter = new AdaptiveConcurrencyLimiter(opts.InitialMaxConcurrency, logger);

        var controller = new AdaptiveTrafficController(
            telemetry,
            new IAdaptiveController[] { rateLimiter, concurrencyLimiter },
            circuitBreaker: inner,
            options: opts,
            logger: logger);

        var decorator = new AdaptiveCircuitBreakerDecorator(
            inner, telemetry, rateLimiter, concurrencyLimiter, controller);

        controller.Start();
        return decorator;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action) =>
        await ExecuteCoreAsync(_ => action(), default).ConfigureAwait(false);

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default) =>
        await ExecuteCoreAsync(action, cancellationToken).ConfigureAwait(false);

    public Task ExecuteAsync(Func<Task> action) =>
        ExecuteAsync<object?>(async () =>
        {
            await action().ConfigureAwait(false);
            return null;
        });

    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
        ExecuteAsync<object?>(async ct =>
        {
            await action(ct).ConfigureAwait(false);
            return null;
        }, cancellationToken);

    private async Task<T> ExecuteCoreAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var succeeded = false;
        var isTimeout = false;

        var concurrencyHeld = false;
        try
        {
            if (!await _concurrencyLimiter.TryAcquireAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ConcurrencyLimitExceededException("Concurrency limit reached due to service stress");
            }

            concurrencyHeld = true;

            if (!await _rateLimiter.TryAcquireAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new RateLimitExceededException("Rate limit exceeded due to service degradation");
            }

            try
            {
                var result = await _inner.ExecuteAsync(action, cancellationToken).ConfigureAwait(false);
                succeeded = true;
                return result;
            }
            catch (TimeoutException)
            {
                isTimeout = true;
                throw;
            }
            catch (Exception)
            {
                succeeded = false;
                throw;
            }
        }
        finally
        {
            if (concurrencyHeld)
            {
                _concurrencyLimiter.Release();
            }

            var elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds;
            _telemetry.RecordExecution(succeeded, elapsedMs, isTimeout);
        }
    }

    public Task<TelemetrySnapshot> GetLatestTelemetryAsync(CancellationToken cancellationToken = default) =>
        _controller.GetLatestTelemetryAsync(cancellationToken);

    public async ValueTask DisposeAsync() => await _controller.DisposeAsync().ConfigureAwait(false);
}
