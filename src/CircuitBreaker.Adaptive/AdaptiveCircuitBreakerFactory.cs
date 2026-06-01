using CircuitBreaker.Core;
using Microsoft.Extensions.Logging;

namespace CircuitBreaker.Adaptive;

/// <summary>
/// Builds a Polly-backed circuit breaker wrapped with adaptive traffic control.
/// </summary>
public static class AdaptiveCircuitBreakerFactory
{
    public static AdaptiveCircuitBreakerDecorator Create(
        CircuitBreakerOptions circuitOptions,
        AdaptiveTrafficControlOptions? adaptiveOptions = null,
        string resourceName = "Default",
        ILogger? logger = null)
    {
        var inner = CircuitBreakerFactory.Create(circuitOptions, resourceName);
        return AdaptiveCircuitBreakerDecorator.Create(inner, adaptiveOptions, logger);
    }

    public static AdaptiveCircuitBreakerDecorator Wrap(
        ICircuitBreaker inner,
        AdaptiveTrafficControlOptions? adaptiveOptions = null,
        ILogger? logger = null) =>
        AdaptiveCircuitBreakerDecorator.Create(inner, adaptiveOptions, logger);
}
