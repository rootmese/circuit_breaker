namespace CircuitBreaker.Adaptive;

/// <summary>
/// Continuous health indicator (0 = dead, 1 = healthy).
/// </summary>
public readonly struct HealthScore
{
    public double Value { get; }

    public HealthScore(double value) => Value = Math.Clamp(value, 0.0, 1.0);

    public static HealthScore Healthy() => new(1.0);
    public static HealthScore Degraded() => new(0.5);
    public static HealthScore Critical() => new(0.2);
    public static HealthScore Dead() => new(0.0);

    public bool IsHealthy => Value >= 0.8;
    public bool IsDegraded => Value is >= 0.4 and < 0.8;
    public bool IsCritical => Value is >= 0.1 and < 0.4;
    public bool IsDead => Value < 0.1;

    public override string ToString() => Value.ToString("F2");
}
