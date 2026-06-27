using CircuitBreaker.AspNetCore.Extensions.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CircuitBreaker.AspNetCore.Extensions;

public static class CircuitBreakerServiceExtensions
{
    public static IHealthChecksBuilder AddCircuitBreakerHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "circuit_breaker",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck<CircuitBreakerHealthCheck>(
            name,
            failureStatus,
            tags ?? Array.Empty<string>());
    }
}