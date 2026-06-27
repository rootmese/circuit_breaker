using CircuitBreaker.Adaptive;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

namespace CircuitBreaker.AspNetCore.Controllers;

/// <summary>
/// API controller to expose circuit breaker telemetry.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CircuitBreakerController : ControllerBase
{
    private readonly AdaptiveCircuitBreakerDecorator _breaker;

    public CircuitBreakerController(AdaptiveCircuitBreakerDecorator breaker)
    {
        _breaker = breaker;
    }

    /// <summary>
    /// Retrieves current telemetry snapshot from the circuit breaker.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetStats()
    {
        var snapshot = await _breaker.GetLatestTelemetryAsync();
        return Ok(new
        {
            _breaker.ResourceName,
            _breaker.State,
            HealthScore = _breaker.CurrentHealthScore.Value,
            snapshot.ErrorRate,
            snapshot.Throughput,
            snapshot.LatencyMs,
            snapshot.P99LatencyMs,
            snapshot.TimeoutRate,
            snapshot.ResourceSaturation,
            snapshot.ActiveConnections,
            snapshot.Timestamp
        });
    }
}