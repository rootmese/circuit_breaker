using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CircuitBreaker.Adaptive;

/// <summary>
/// In-flight request gate with a dynamically adjustable concurrency ceiling.
/// </summary>
public sealed class AdaptiveConcurrencyLimiter : IAdaptiveController
{
    private readonly ILogger _logger;
    private int _maxConcurrency;
    private int _inFlight;

    public string Name => "ConcurrencyControl";

    public AdaptiveConcurrencyLimiter(int initialMaxConcurrency = 100, ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _maxConcurrency = initialMaxConcurrency;
    }

    public int CurrentMaxConcurrency => Volatile.Read(ref _maxConcurrency);

    public Task ApplyControlAsync(HealthScore score, CancellationToken cancellationToken = default)
    {
        var newLimit = HealthScoreTrafficTiers.MapToConcurrency(score);
        var current = CurrentMaxConcurrency;

        if (newLimit != current)
        {
            _logger.LogWarning(
                "Concurrency: {Old} → {New} (health {Score:F2}, in-flight {InFlight})",
                current, newLimit, score.Value, Volatile.Read(ref _inFlight));
            Volatile.Write(ref _maxConcurrency, newLimit);
        }

        return Task.CompletedTask;
    }

    public ValueTask<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var max = CurrentMaxConcurrency;
        if (max <= 0)
        {
            return ValueTask.FromResult(false);
        }

        // Atomic check-then-increment to prevent race condition
        var current = Volatile.Read(ref _inFlight);
        while (current < max)
        {
            if (Interlocked.CompareExchange(ref _inFlight, current + 1, current) == current)
            {
                return ValueTask.FromResult(true);
            }
            current = Volatile.Read(ref _inFlight);
        }

        return ValueTask.FromResult(false);
    }

    public void Release()
    {
        Interlocked.Decrement(ref _inFlight);
    }
}
