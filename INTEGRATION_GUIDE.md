# ASP.NET Core Integration Guide

This guide demonstrates how to integrate the **CircuitBreaker** library and its telemetry pipeline into an ASP.NET Core application using the `CircuitBreaker.AspNetCore.Extensions` package.

---

## 1. Installation

Make sure your web application references the following projects/packages:
- `CircuitBreaker.Core`
- `CircuitBreaker.Telemetry`
- `CircuitBreaker.Adaptive`
- `CircuitBreaker.AspNetCore.Extensions`

---

## 2. Configuration in `Program.cs`

In your API or Web Application startup file, configure the Circuit Breaker, register any metrics exporters (plugins), and start the background telemetry publisher.

```csharp
using CircuitBreaker.Core;
using CircuitBreaker.Adaptive;
using CircuitBreaker.Adaptive.DependencyInjection;
using CircuitBreaker.Telemetry;
using CircuitBreaker.Telemetry.DependencyInjection;
using CircuitBreaker.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Controllers if exposing the telemetry endpoint via Controller
builder.Services.AddControllers();

// 2. Register the Adaptive Circuit Breaker
builder.Services.AddAdaptiveCircuitBreaker(
    circuitOptions: new CircuitBreakerOptions
    {
        FailureRatio = 0.5,             // Trips when 50% of requests fail
        MinimumThroughput = 8,          // Minimum requests evaluated
        SamplingDuration = TimeSpan.FromSeconds(10),
        BreakDuration = TimeSpan.FromSeconds(15)
    },
    adaptiveOptions: new AdaptiveTrafficControlOptions
    {
        InitialMaxRequestsPerSecond = 500,
        InitialMaxConcurrency = 50
    },
    resourceName: "PaymentAPI"
);

// 3. Register your custom metrics exporters (Plugins)
// Any class implementing ICircuitBreakerMetricsExporter will be executed in parallel.
builder.Services.AddCircuitBreakerExporter<ConsoleMetricsExporter>();
// builder.Services.AddCircuitBreakerExporter<PrometheusMetricsExporter>(); // Example external exporter

// 4. Start background telemetry publishing (runs every 5 seconds)
builder.Services.AddCircuitBreakerTelemetry(exportInterval: TimeSpan.FromSeconds(5));

// 5. Configure ASP.NET Core Health Checks
builder.Services.AddHealthChecks()
    .AddCircuitBreakerHealthCheck(name: "circuit_breaker_health");

var app = builder.Build();

// 6. Map Controllers (required to expose 'api/CircuitBreaker/stats')
app.MapControllers();

// 7. Map Health Check endpoint
app.MapHealthChecks("/health");

app.Run();
```

---

## 3. Implementing a Custom Exporter Plugin

Export plugins allow you to push Circuit Breaker metrics to external telemetry collectors (like Prometheus, OpenTelemetry, Zabbix, databases, or local logging). To write your own plugin, implement the `ICircuitBreakerMetricsExporter` interface:

```csharp
using CircuitBreaker.Telemetry;

public class ConsoleMetricsExporter : ICircuitBreakerMetricsExporter
{
    private readonly ILogger<ConsoleMetricsExporter> _logger;

    public ConsoleMetricsExporter(ILogger<ConsoleMetricsExporter> logger)
    {
        _logger = logger;
    }

    public Task ExportAsync(CircuitBreakerSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[{Timestamp}] Resource: {ResourceName} | State: {State} | Health Score: {HealthScore:P0} | Throughput: {Throughput}/s | P99 Latency: {P99LatencyMs}ms",
            snapshot.Timestamp,
            snapshot.ResourceName,
            snapshot.State,
            snapshot.HealthScore,
            snapshot.Throughput,
            snapshot.P99LatencyMs
        );

        return Task.CompletedTask;
    }
}
```

---

## 4. Exposed Endpoints

Once integrated, your API will automatically expose the following diagnostics endpoints:

### Telemetry Stats Endpoint (Controller-based)
- **Route**: `/api/CircuitBreaker/stats`
- **Method**: `GET`
- **Response**: JSON payload with real-time statistics (`ErrorRate`, `Throughput`, `LatencyMs`, `P99LatencyMs`, `ActiveConnections`, `State`, `HealthScore`).

### ASP.NET Core Health Check
- **Route**: `/health`
- **Method**: `GET`
- **States**:
  - `200 OK` (Healthy) if the health score is $\ge 0.8$.
  - `200 OK` (Degraded) if the health score is between $0.4$ and $0.8$.
  - `503 Service Unavailable` (Unhealthy) if the health score drops below $0.4$.
