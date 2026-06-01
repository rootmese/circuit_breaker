using CircuitBreaker.Telemetry;

namespace CircuitBreaker.Adaptive;

/// <summary>
/// Weighted health score from telemetry snapshots.
/// </summary>
public sealed class HealthScoreCalculator
{
    private readonly Dictionary<string, double> _weights = new()
    {
        ["ErrorRate"] = 0.35,
        ["LatencyMs"] = 0.20,
        ["Throughput"] = 0.15,
        ["P99LatencyMs"] = 0.15,
        ["TimeoutRate"] = 0.10,
        ["ResourceSaturation"] = 0.05
    };

    private readonly Dictionary<string, (double Healthy, double Warning, double Critical)> _thresholds = new()
    {
        ["ErrorRate"] = (0.05, 0.10, 0.25),
        ["LatencyMs"] = (100, 200, 500),
        ["Throughput"] = (1000, 500, 100),
        ["P99LatencyMs"] = (200, 400, 800),
        ["TimeoutRate"] = (0.02, 0.05, 0.10),
        ["ResourceSaturation"] = (0.3, 0.6, 0.8)
    };

    public HealthScore Calculate(TelemetrySnapshot telemetry)
    {
        var scores = new Dictionary<string, double>
        {
            ["ErrorRate"] = MapToZone(telemetry.ErrorRate, _thresholds["ErrorRate"], lowerIsBetter: true),
            ["LatencyMs"] = MapToZone(telemetry.LatencyMs, _thresholds["LatencyMs"], lowerIsBetter: true),
            ["P99LatencyMs"] = MapToZone(telemetry.P99LatencyMs, _thresholds["P99LatencyMs"], lowerIsBetter: true),
            ["TimeoutRate"] = MapToZone(telemetry.TimeoutRate, _thresholds["TimeoutRate"], lowerIsBetter: true),
            ["ResourceSaturation"] = MapToZone(telemetry.ResourceSaturation, _thresholds["ResourceSaturation"], lowerIsBetter: true),
            ["Throughput"] = MapToZone(telemetry.Throughput, _thresholds["Throughput"], lowerIsBetter: false)
        };

        double totalScore = 0;
        double totalWeight = 0;

        foreach (var (metric, weight) in _weights)
        {
            totalScore += scores[metric] * weight;
            totalWeight += weight;
        }

        return new HealthScore(totalScore / totalWeight);
    }

    public void ConfigureWeight(string metric, double weight)
    {
        if (_weights.ContainsKey(metric))
        {
            _weights[metric] = weight;
        }
    }

    public void ConfigureThreshold(string metric, double healthy, double warning, double critical)
    {
        _thresholds[metric] = (healthy, warning, critical);
    }

    private static double MapToZone(
        double value,
        (double Healthy, double Warning, double Critical) thresholds,
        bool lowerIsBetter)
    {
        if (lowerIsBetter)
        {
            if (value <= thresholds.Healthy) return 1.0;
            if (value <= thresholds.Warning)
            {
                return Math.Clamp(
                    1.0 - (value - thresholds.Healthy) / (thresholds.Warning - thresholds.Healthy) * 0.2,
                    0.0,
                    1.0);
            }

            if (value <= thresholds.Critical)
            {
                return Math.Clamp(
                    0.8 - (value - thresholds.Warning) / (thresholds.Critical - thresholds.Warning) * 0.3,
                    0.0,
                    1.0);
            }

            return Math.Clamp(
                0.2 - (value - thresholds.Critical) / Math.Max(thresholds.Critical, 1.0) * 0.2,
                0.0,
                1.0);
        }

        if (value >= thresholds.Healthy) return 1.0;
        if (value >= thresholds.Warning)
        {
            return Math.Clamp(
                1.0 - (thresholds.Healthy - value) / Math.Max(thresholds.Healthy - thresholds.Warning, 1.0) * 0.2,
                0.0,
                1.0);
        }

        if (value >= thresholds.Critical)
        {
            return Math.Clamp(
                0.8 - (thresholds.Warning - value) / Math.Max(thresholds.Warning - thresholds.Critical, 1.0) * 0.3,
                0.0,
                1.0);
        }

        return Math.Clamp(
            0.2 - (thresholds.Critical - value) / Math.Max(Math.Abs(thresholds.Critical), 1.0) * 0.2,
            0.0,
            1.0);
    }
}
