namespace CircuitBreaker.Adaptive;

/// <summary>
/// Configuration for adaptive traffic control and the decorator.
/// </summary>
public sealed class AdaptiveTrafficControlOptions
{
    public TimeSpan TelemetryWindow { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ControlLoopInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public double SuddenDegradationDelta { get; set; } = 0.3;
    public int InitialMaxRequestsPerSecond { get; set; } = 1000;
    public int InitialMaxConcurrency { get; set; } = 100;
    public HealthScore EmergencyHealthScore { get; set; } = new(0.15);
}
