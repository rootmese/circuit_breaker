using System.Collections.Concurrent;

namespace CircuitBreaker.Telemetry;

/// <summary>
/// Thread-safe rolling-window telemetry using time-based segments for O(1) cleanup.
/// Uses bucketing to avoid O(n) scans on every collection.
/// </summary>
public sealed class RollingWindowTelemetryProvider : ITelemetryProvider, IExecutionTelemetryRecorder
{
    private readonly ConcurrentDictionary<long, Bucket> _buckets = new();
    private readonly TimeSpan _windowSize;
    private readonly TimeSpan _bucketDuration = TimeSpan.FromSeconds(1);

    public RollingWindowTelemetryProvider(TimeSpan? windowSize = null, int maxRecords = 10_000)
    {
        _windowSize = windowSize ?? TimeSpan.FromSeconds(30);
    }

    public void RecordExecution(bool succeeded, double latencyMs, bool isTimeout = false)
    {
        var now = DateTime.UtcNow;
        var bucketKey = now.Ticks / _bucketDuration.Ticks;
        
        _buckets.AddOrUpdate(bucketKey,
            _ => new Bucket { Records = new List<ExecutionRecord> { new(now, succeeded, latencyMs, isTimeout) } },
            (_, bucket) =>
            {
                lock (bucket.Lock)
                {
                    bucket.Records.Add(new ExecutionRecord(now, succeeded, latencyMs, isTimeout));
                }
                return bucket;
            });

        // Cleanup old buckets (O(k) where k = number of expired buckets, not O(n) total records)
        var cutoffBucket = (DateTime.UtcNow - _windowSize).Ticks / _bucketDuration.Ticks;
        var oldBuckets = _buckets.Keys.Where(k => k < cutoffBucket).ToList();
        foreach (var key in oldBuckets)
        {
            _buckets.TryRemove(key, out _);
        }
    }

    public Task<TelemetrySnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cutoff = DateTime.UtcNow - _windowSize;
        var window = new List<ExecutionRecord>();

        // Collect only from valid time-based buckets
        var cutoffBucket = cutoff.Ticks / _bucketDuration.Ticks;
        foreach (var (bucketKey, bucket) in _buckets)
        {
            if (bucketKey >= cutoffBucket)
            {
                lock (bucket.Lock)
                {
                    window.AddRange(bucket.Records.Where(r => r.Timestamp >= cutoff));
                }
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
        public DateTime Timestamp { get; }
        public bool Succeeded { get; }
        public double LatencyMs { get; }
        public bool IsTimeout { get; }

        public ExecutionRecord(DateTime timestamp, bool succeeded, double latencyMs, bool isTimeout)
        {
            Timestamp = timestamp;
            Succeeded = succeeded;
            LatencyMs = latencyMs;
            IsTimeout = isTimeout;
        }
    }

    private sealed class Bucket
    {
        public object Lock { get; } = new();
        public List<ExecutionRecord> Records { get; set; } = new();
    }
}
