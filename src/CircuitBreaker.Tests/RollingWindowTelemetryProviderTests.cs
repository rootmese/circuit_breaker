using CircuitBreaker.Telemetry;

namespace CircuitBreaker.Tests;

public class RollingWindowTelemetryProviderTests
{
    [Fact]
    public async Task RecordExecution_ShouldRecordSuccessfulExecution()
    {
        // Arrange
        var provider = new RollingWindowTelemetryProvider(TimeSpan.FromSeconds(30));

        // Act
        provider.RecordExecution(succeeded: true, latencyMs: 50, isTimeout: false);
        var snapshot = await provider.CollectAsync();

        // Assert
        Assert.Equal(0, snapshot.ErrorRate);
    }

    [Fact]
    public async Task RecordExecution_ShouldTrackErrorRate()
    {
        // Arrange
        var provider = new RollingWindowTelemetryProvider(TimeSpan.FromSeconds(30));

        // Act
        provider.RecordExecution(succeeded: true, latencyMs: 50);
        provider.RecordExecution(succeeded: false, latencyMs: 100);
        provider.RecordExecution(succeeded: false, latencyMs: 120);
        var snapshot = await provider.CollectAsync();

        // Assert
        Assert.Equal(2.0 / 3.0, snapshot.ErrorRate, precision: 2);
    }

    [Fact]
    public async Task RecordExecution_ShouldCalculateAverageLatency()
    {
        // Arrange
        var provider = new RollingWindowTelemetryProvider(TimeSpan.FromSeconds(30));

        // Act
        provider.RecordExecution(succeeded: true, latencyMs: 100);
        provider.RecordExecution(succeeded: true, latencyMs: 200);
        provider.RecordExecution(succeeded: true, latencyMs: 300);
        var snapshot = await provider.CollectAsync();

        // Assert
        Assert.Equal(200, snapshot.LatencyMs, precision: 0);
    }

    [Fact]
    public async Task RecordExecution_ShouldTrackTimeoutRate()
    {
        // Arrange
        var provider = new RollingWindowTelemetryProvider(TimeSpan.FromSeconds(30));

        // Act
        provider.RecordExecution(succeeded: false, latencyMs: 50, isTimeout: true);
        provider.RecordExecution(succeeded: false, latencyMs: 60, isTimeout: true);
        provider.RecordExecution(succeeded: true, latencyMs: 100, isTimeout: false);
        var snapshot = await provider.CollectAsync();

        // Assert
        Assert.Equal(2.0 / 3.0, snapshot.TimeoutRate, precision: 2);
    }

    [Fact]
    public async Task CollectAsync_WithNoRecords_ReturnsZeroMetrics()
    {
        // Arrange
        var provider = new RollingWindowTelemetryProvider(TimeSpan.FromSeconds(30));

        // Act
        var snapshot = await provider.CollectAsync();

        // Assert
        Assert.Equal(0, snapshot.Throughput);
    }

    [Fact]
    public async Task CollectAsync_ShouldCleanupOldBuckets()
    {
        // Arrange
        var windowSize = TimeSpan.FromMilliseconds(500);
        var provider = new RollingWindowTelemetryProvider(windowSize);

        // Act
        provider.RecordExecution(succeeded: true, latencyMs: 50);
        await Task.Delay(600);
        provider.RecordExecution(succeeded: true, latencyMs: 60);
        var snapshot = await provider.CollectAsync();

        // Assert - Only the new record should be in window
        Assert.Equal(1, snapshot.Throughput);
    }

    [Fact]
    public async Task RecordExecution_ShouldCalculatePercentile()
    {
        // Arrange
        var provider = new RollingWindowTelemetryProvider(TimeSpan.FromSeconds(30));

        // Act - Record 100 values from 1-100ms
        for (int i = 1; i <= 100; i++)
        {
            provider.RecordExecution(succeeded: true, latencyMs: i);
        }
        var snapshot = await provider.CollectAsync();

        // Assert - P99 should be around 99ms
        Assert.InRange(snapshot.P99LatencyMs, 95, 105);
    }

    [Fact]
    public async Task CollectAsync_IsThreadSafe()
    {
        // Arrange
        var provider = new RollingWindowTelemetryProvider(TimeSpan.FromSeconds(30));
        var tasks = new List<Task>();

        // Act - Record from multiple threads
        for (int t = 0; t < 10; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    provider.RecordExecution(succeeded: i % 2 == 0, latencyMs: Random.Shared.Next(10, 100));
                }
            }));
        }

        await Task.WhenAll(tasks);
        var snapshot = await provider.CollectAsync();

        // Assert
        Assert.Equal(1000, snapshot.Throughput);
    }
}
