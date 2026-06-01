using System.Collections.Concurrent;

namespace CircuitBreaker.Telemetry;

/// <summary>
/// Thread-safe rolling-window telemetry derived from recorded executions only (no duplicate counters).
/// </summary>
public sealed class RollingWindowTelemetryProvider : ITelemetryProvider, IExecutionTelemetryRecorder
{
    private readonly ConcurrentQueue<ExecutionRecord> _records = new();
    private readonly TimeSpan _windowSize;
    private readonly int _maxRecords;

    public RollingWindowTelemetryProvider(TimeSpan? windowSize = null, int maxRecords = 10_000)
    {
        _windowSize = windowSize ?? TimeSpan.FromSeconds(30);
        _maxRecords = maxRecords;
    }

    public void RecordExecution(bool succeeded, double latencyMs, bool isTimeout = false)
    {
        _records.Enqueue(new ExecutionRecord
        {
            Timestamp = DateTime.UtcNow,
            Succeeded = succeeded,
            LatencyMs = latencyMs,
            IsTimeout = isTimeout
        });

        while (_records.Count > _maxRecords && _records.TryDequeue(out _))
        {
        }
    }

    public Task<TelemetrySnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cutoff = DateTime.UtcNow - _windowSize;
        var window = new List<ExecutionRecord>();

        foreach (var record in _records)
        {
            if (record.Timestamp >= cutoff)
            {
                window.Add(record);
            }
        }

        if (window.Count == 0)
        {
            return Task.FromResult(new TelemetrySnapshot
            {
                Timestamp = DateTime.UtcNow
            });
        }

        var total = window.Count;
        var failed = window.Count(r => !r.Succeeded);
        var timeouts = window.Count(r => r.IsTimeout);
        var latencies = window.Select(r => r.LatencyMs).ToList();
        var oneSecondAgo = DateTime.UtcNow - TimeSpan.FromSeconds(1);
        var throughput = window.Count(r => r.Timestamp >= oneSecondAgo);
        var fiveSecondsAgo = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        var active = window.Count(r => r.Timestamp >= fiveSecondsAgo);

        return Task.FromResult(new TelemetrySnapshot
        {
            ErrorRate = (double)failed / total,
            TimeoutRate = (double)timeouts / total,
            LatencyMs = latencies.Average(),
            P99LatencyMs = Percentile(latencies, 0.99),
            Throughput = throughput,
            ResourceSaturation = EstimateResourceSaturation(),
            ActiveConnections = active,
            Timestamp = DateTime.UtcNow
        });
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(v => v).ToArray();
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        index = Math.Clamp(index, 0, sorted.Length - 1);
        return sorted[index];
    }

    private static double EstimateResourceSaturation()
    {
        var threads = ThreadPool.ThreadCount;
        var pending = ThreadPool.PendingWorkItemCount;
        return Math.Min(pending / (double)(threads + 1), 1.0);
    }

    private sealed class ExecutionRecord
    {
        public DateTime Timestamp { get; init; }
        public bool Succeeded { get; init; }
        public double LatencyMs { get; init; }
        public bool IsTimeout { get; init; }
    }
}
