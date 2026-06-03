using CircuitBreaker.Adaptive;
using CircuitBreaker.Core;
using CircuitBreaker.Telemetry;

namespace CircuitBreaker.Tests;

public class AdaptiveConcurrencyLimiterTests
{
    [Fact]
    public async Task TryAcquireAsync_UnderLimit_ReturnsTrue()
    {
        // Arrange
        var limiter = new AdaptiveConcurrencyLimiter(initialMaxConcurrency: 5);

        // Act
        var result = await limiter.TryAcquireAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TryAcquireAsync_AtLimit_ReturnsFalse()
    {
        // Arrange
        var limiter = new AdaptiveConcurrencyLimiter(initialMaxConcurrency: 2);

        // Act
        var result1 = await limiter.TryAcquireAsync();
        var result2 = await limiter.TryAcquireAsync();
        var result3 = await limiter.TryAcquireAsync();

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.False(result3);
    }

    [Fact]
    public async Task Release_AllowsNewAcquisitions()
    {
        // Arrange
        var limiter = new AdaptiveConcurrencyLimiter(initialMaxConcurrency: 1);
        await limiter.TryAcquireAsync();

        // Act
        limiter.Release();
        var result = await limiter.TryAcquireAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TryAcquireAsync_IsAtomicSafe()
    {
        // Arrange
        var limiter = new AdaptiveConcurrencyLimiter(initialMaxConcurrency: 50);
        var successCount = 0;
        var failCount = 0;
        var lockObj = new object();

        // Act - Hammer with 100 concurrent tasks
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                if (await limiter.TryAcquireAsync())
                {
                    lock (lockObj)
                    {
                        successCount++;
                    }
                }
                else
                {
                    lock (lockObj)
                    {
                        failCount++;
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Exactly 50 should succeed, rest should fail
        Assert.Equal(50, successCount);
        Assert.Equal(50, failCount);
    }

    [Fact]
    public async Task ApplyControlAsync_ReducesLimitOnDegradation()
    {
        // Arrange
        var limiter = new AdaptiveConcurrencyLimiter(initialMaxConcurrency: 100);
        var degradedScore = new HealthScore(0.5);

        // Act
        await limiter.ApplyControlAsync(degradedScore);

        // Assert
        Assert.True(limiter.CurrentMaxConcurrency < 100);
    }
}

public class AdaptiveRateLimiterTests
{
    [Fact]
    public async Task TryAcquireAsync_UnderRateLimit_ReturnsTrue()
    {
        // Arrange
        var limiter = new AdaptiveRateLimiter(initialPermitsPerSecond: 1000);

        // Act
        var result = await limiter.TryAcquireAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ApplyControlAsync_ReducesPermitsOnDegradation()
    {
        // Arrange
        var limiter = new AdaptiveRateLimiter(initialPermitsPerSecond: 1000);
        var degradedScore = new HealthScore(0.3);

        // Act
        await limiter.ApplyControlAsync(degradedScore);

        // Assert
        Assert.True(limiter.CurrentPermitsPerSecond < 1000);
    }
}

public class HealthScoreCalculatorTests
{
    [Fact]
    public void Calculate_WithHealthyTelemetry_ReturnsHighScore()
    {
        // Arrange
        var calculator = new HealthScoreCalculator();
        var telemetry = new TelemetrySnapshot
        {
            ErrorRate = 0.01,
            LatencyMs = 50,
            P99LatencyMs = 100,
            TimeoutRate = 0.0,
            Throughput = 2000,
            ResourceSaturation = 0.2
        };

        // Act
        var score = calculator.Calculate(telemetry);

        // Assert
        Assert.True(score.IsHealthy, $"Score {score} should be healthy");
        Assert.InRange(score.Value, 0.8, 1.0);
    }

    [Fact]
    public void Calculate_WithDegradedTelemetry_ReturnsMidScore()
    {
        // Arrange
        var calculator = new HealthScoreCalculator();
        var telemetry = new TelemetrySnapshot
        {
            ErrorRate = 0.15,
            LatencyMs = 300,
            P99LatencyMs = 500,
            TimeoutRate = 0.05,
            Throughput = 500,
            ResourceSaturation = 0.6
        };

        // Act
        var score = calculator.Calculate(telemetry);

        // Assert
        Assert.True(score.IsDegraded, $"Score {score} should be degraded");
        Assert.InRange(score.Value, 0.4, 0.8);
    }

    [Fact]
    public void Calculate_WithCriticalTelemetry_ReturnsLowScore()
    {
        // Arrange
        var calculator = new HealthScoreCalculator();
        var telemetry = new TelemetrySnapshot
        {
            ErrorRate = 0.5,
            LatencyMs = 600,
            P99LatencyMs = 1000,
            TimeoutRate = 0.15,
            Throughput = 50,
            ResourceSaturation = 0.9
        };

        // Act
        var score = calculator.Calculate(telemetry);

        // Assert
        Assert.True(score.IsCritical || score.IsDead, $"Score {score} should be critical or dead");
    }

    [Fact]
    public void ConfigureThreshold_WithInvalidThresholds_ThrowsException()
    {
        // Arrange
        var calculator = new HealthScoreCalculator();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            calculator.ConfigureThreshold("ErrorRate", healthy: 0.2, warning: 0.1, critical: 0.05)
        );
    }

    [Fact]
    public void HealthScore_Clamps_ToZeroOne()
    {
        // Arrange & Act
        var scoreNegative = new HealthScore(-0.5);
        var scoreOverOne = new HealthScore(1.5);

        // Assert
        Assert.Equal(0.0, scoreNegative.Value);
        Assert.Equal(1.0, scoreOverOne.Value);
    }

    [Fact]
    public void HealthScore_PredefineValues_AreCorrect()
    {
        // Assert
        Assert.Equal(1.0, HealthScore.Healthy().Value);
        Assert.Equal(0.5, HealthScore.Degraded().Value);
        Assert.Equal(0.2, HealthScore.Critical().Value);
        Assert.Equal(0.0, HealthScore.Dead().Value);
    }
}
