using CircuitBreaker.Adaptive;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CircuitBreaker.AspNetCore.Extensions.HealthChecks;

public class CircuitBreakerHealthCheck : IHealthCheck
{
    private readonly AdaptiveCircuitBreakerDecorator _breaker;

    public CircuitBreakerHealthCheck(AdaptiveCircuitBreakerDecorator breaker)
    {
        _breaker = breaker;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _breaker.GetLatestTelemetryAsync(cancellationToken);
        var score = _breaker.CurrentHealthScore;

        // Cria o dicionário de dados
        var data = new Dictionary<string, object>
        {
            ["ErrorRate"] = snapshot.ErrorRate,
            ["Throughput"] = snapshot.Throughput,
            ["LatencyMs"] = snapshot.LatencyMs,
            ["P99LatencyMs"] = snapshot.P99LatencyMs,
            ["TimeoutRate"] = snapshot.TimeoutRate,
            ["Saturation"] = snapshot.ResourceSaturation,
            ["State"] = _breaker.State.ToString(),
            ["HealthScore"] = score.Value
        };

        // Converte explicitamente para IReadOnlyDictionary
        IReadOnlyDictionary<string, object> readOnlyData = data;

        if (score.IsHealthy)
            return HealthCheckResult.Healthy("Circuit breaker is healthy", readOnlyData);

        if (score.IsDegraded)
            return HealthCheckResult.Degraded("Circuit breaker is degraded", data: readOnlyData);

        return HealthCheckResult.Unhealthy("Circuit breaker is unhealthy", data: readOnlyData);
    }
}