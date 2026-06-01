namespace CircuitBreaker.Adaptive;

/// <summary>
/// Applies a traffic-control measure based on the current health score.
/// </summary>
public interface IAdaptiveController
{
    string Name { get; }

    Task ApplyControlAsync(HealthScore score, CancellationToken cancellationToken = default);
}
