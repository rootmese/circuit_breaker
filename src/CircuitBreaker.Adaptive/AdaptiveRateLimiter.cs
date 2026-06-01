using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CircuitBreaker.Adaptive;

/// <summary>
/// Token-bucket rate limiter adjusted by health score (requests per second).
/// </summary>
public sealed class AdaptiveRateLimiter : IAdaptiveController, IDisposable
{
    private readonly object _sync = new();
    private readonly ILogger _logger;
    private TokenBucketRateLimiter? _limiter;
    private int _permitsPerSecond;

    public string Name => "RateLimiting";

    public AdaptiveRateLimiter(int initialPermitsPerSecond = 1000, ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        SetPermitLimit(initialPermitsPerSecond);
    }

    public int CurrentPermitsPerSecond => Volatile.Read(ref _permitsPerSecond);

    public Task ApplyControlAsync(HealthScore score, CancellationToken cancellationToken = default)
    {
        var newLimit = HealthScoreTrafficTiers.MapToRateLimit(score);
        if (newLimit != CurrentPermitsPerSecond)
        {
            _logger.LogWarning(
                "Rate limit: {Old} → {New} req/s (health {Score:F2})",
                CurrentPermitsPerSecond, newLimit, score.Value);
            SetPermitLimit(newLimit);
        }

        return Task.CompletedTask;
    }

    public async ValueTask<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentPermitsPerSecond <= 0)
        {
            return false;
        }

        TokenBucketRateLimiter? limiter;
        lock (_sync)
        {
            limiter = _limiter;
        }

        if (limiter is null)
        {
            return false;
        }

        using var lease = await limiter.AcquireAsync(permitCount: 1, cancellationToken);
        return lease.IsAcquired;
    }

    private void SetPermitLimit(int permitsPerSecond)
    {
        lock (_sync)
        {
            if (permitsPerSecond == _permitsPerSecond && _limiter is not null)
            {
                return;
            }

            _limiter?.Dispose();
            _permitsPerSecond = permitsPerSecond;

            if (permitsPerSecond <= 0)
            {
                _limiter = null;
                return;
            }

            _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = permitsPerSecond,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokensPerPeriod = permitsPerSecond,
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _limiter?.Dispose();
            _limiter = null;
        }
    }
}
