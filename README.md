# CircuitBreaker - Production-Ready Resilience Library

> .NET library that wraps Polly v8's **Advanced Circuit Breaker** in a simple, consistent API with adaptive traffic control, pluggable telemetry export, and production-grade reliability.

**License**: MIT  
**Status**: Production Ready | `net8.0` + `net9.0`

---

## Overview

This repository contains a battle-tested abstraction layer over Polly's Circuit Breaker with:

- ✅ **Simplified API** - Clean, intuitive interface
- ✅ **Async/Await Native** - Full `CancellationToken` support
- ✅ **Telemetry-First** - Real-time metrics collection (O(k) optimized)
- ✅ **Adaptive Control** - Dynamic rate limiting & concurrency management
- ✅ **Pluggable Export** - Send metrics to Prometheus, OpenTelemetry, Zabbix, or any custom sink
- ✅ **Thread-Safe** - Atomic operations, no race conditions
- ✅ **Fail-Safe** - Guaranteed resource release (try/finally) preventing deadlocks under extreme load
- ✅ **DI Compatible** - Microsoft.Extensions.DependencyInjection ready
- ✅ **Well-Tested** - Unit tests with full pass rate
- ✅ **Production-Documented** - Complete tuning guide included

### Included Projects

| Project | Purpose | Status |
|---------|---------|--------|
| `CircuitBreaker.Core` | Polly v8 wrapper + state management | ✅ |
| `CircuitBreaker.Telemetry` | Rolling-window metrics + snapshot export pipeline | ✅ |
| `CircuitBreaker.Adaptive` | Adaptive rate limiting + concurrency | ✅ |
| `CircuitBreaker.Sample` | Core usage example | ✅ |
| `CircuitBreaker.Adaptive.Sample` | Advanced adaptive demo | ✅ |
| `CircuitBreaker.Tests` | Unit tests | ✅ |

---

## Quick Start

### 1. Install

```bash
dotnet restore src/CircuitBreaker.slnx
```

### 2. Basic Usage (Circuit Breaker Only)

```csharp
var breaker = CircuitBreakerFactory.Create(
    new CircuitBreakerOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 8,
        SamplingDuration = TimeSpan.FromSeconds(10),
        BreakDuration = TimeSpan.FromSeconds(5),
        OnOpened = d => Console.WriteLine($"Circuit opened for {d.TotalSeconds}s"),
        OnClosed = () => Console.WriteLine("Circuit closed"),
        OnHalfOpened = () => Console.WriteLine("Circuit half-open - testing...")
    }
);

try {
    var result = await breaker.ExecuteAsync(async () =>
        await CallDownstreamServiceAsync());
}
catch (BrokenCircuitException) {
    // Fallback logic
}
```

### 3. Advanced Usage (Adaptive Control)

```csharp
services.AddAdaptiveCircuitBreaker(
    circuitOptions: new CircuitBreakerOptions { /* ... */ },
    adaptiveOptions: new AdaptiveTrafficControlOptions
    {
        InitialMaxRequestsPerSecond = 1000,
        InitialMaxConcurrency = 100,
        ControlLoopInterval = TimeSpan.FromMilliseconds(100)
    }
);
```

### 4. Telemetry Export (New in v0.5.0)

Export live metrics to any external system — Prometheus, OpenTelemetry, Zabbix, dashboards, etc.

```csharp
// Program.cs
using CircuitBreaker.Adaptive.DependencyInjection;
using CircuitBreaker.Telemetry.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Register the adaptive circuit breaker (also registers ICircuitBreakerSnapshotSource)
builder.Services.AddAdaptiveCircuitBreaker(
    circuitOptions: new CircuitBreakerOptions { /* ... */ },
    resourceName: "PaymentAPI");

// 2. Register one or more exporters
builder.Services.AddCircuitBreakerExporter<MyPrometheusExporter>();
// builder.Services.AddCircuitBreakerExporter<MyOpenTelemetryExporter>();

// 3. Start background export every 10 s
builder.Services.AddCircuitBreakerTelemetry(exportInterval: TimeSpan.FromSeconds(10));
```

Implement a custom exporter:

```csharp
public class MyPrometheusExporter : ICircuitBreakerMetricsExporter
{
    public Task ExportAsync(CircuitBreakerSnapshot snapshot, CancellationToken ct)
    {
        // snapshot.ResourceName, snapshot.InstanceId, snapshot.State,
        // snapshot.ErrorRate, snapshot.LatencyMs, snapshot.P99LatencyMs,
        // snapshot.Throughput, snapshot.HealthScore, snapshot.Timestamp …
        return Task.CompletedTask;
    }
}
```

### 5. Run Examples

```bash
# Build all projects
dotnet build src/CircuitBreaker.slnx

# Run core sample
dotnet run --project src/CircuitBreaker.Sample

# Run adaptive sample
dotnet run --project src/CircuitBreaker.Adaptive.Sample

# Run tests
dotnet test src/CircuitBreaker.slnx
```

---

## Architecture

```text
Your Application
      |
      v
IMyService
      |
      v
MyServiceDecorator
      |
      v
AdaptiveCircuitBreakerDecorator   ←─── ICircuitBreakerSnapshotSource
      |                                        |
      v                               CircuitBreakerTelemetryBackgroundService
ICircuitBreaker                               |
      |                               ICircuitBreakerTelemetryPublisher
      v                                       |
ResiliencePipeline               ┌────────────┴──────────────┐
   (Polly v8)              Exporter A               Exporter B …
```

The `CircuitBreaker` acts as a thin wrapper over Polly's `ResiliencePipeline`, adding comprehensive telemetry and adaptive control layers. The export pipeline is completely decoupled — the breaker has zero knowledge of exporters.

### Telemetry Pipeline (v0.5.0)

| Type | Role |
|------|------|
| `CircuitBreakerSnapshot` | Immutable record — single observability contract (`ResourceName`, `InstanceId`, `State`, metrics, `DateTimeOffset`) |
| `ICircuitBreakerSnapshotSource` | Abstraction implemented by `AdaptiveCircuitBreakerDecorator`; decouples Telemetry from Adaptive |
| `ICircuitBreakerMetricsExporter` | Implement to push snapshots to any external system |
| `ICircuitBreakerTelemetryPublisher` | Fans out to all registered exporters in parallel; isolates exporter failures |
| `CircuitBreakerTelemetryBackgroundService` | `BackgroundService` that collects + publishes on a configurable interval |

### Health Scoring

Continuous health indicator (0.0 = dead, 1.0 = healthy) based on:
- **Error Rate** (35% weight)
- **Latency** (20% weight)
- **P99 Latency** (15% weight)
- **Timeout Rate** (10% weight)
- **Resource Saturation** (5% weight)
- **Throughput** (15% weight)

Dynamically adjusts rate limits and concurrency based on health state.

---

## System Requirements

- **.NET 8.0** or **.NET 9.0**
- **Polly 8.6.6+** (included as dependency)
- Optional: `Microsoft.Extensions.DependencyInjection` for DI integration
- Optional: `Microsoft.Extensions.Logging` for structured logging
- Optional: `Microsoft.Extensions.Hosting.Abstractions` for background telemetry export

---

## Testing

```bash
# Run all tests with verbose output
dotnet test src/CircuitBreaker.slnx -v normal

# Run with coverage
dotnet test src/CircuitBreaker.slnx --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test src/CircuitBreaker.slnx --filter "FullyQualifiedName~CircuitBreakerTests"
```

**Test categories**:
- Circuit Breaker state transitions & callbacks
- Telemetry collection & aggregation
- Concurrent operations (race condition verification)
- Health score calculation
- Adaptive rate limiting & concurrency

---

## State Machine

```text
┌──────────┐  failure rate exceeds threshold  ┌──────────┐
│  CLOSED  ├──────────────────────────────────>│   OPEN   │
│          │                                    │          │
└──────────┘  <────────────────────────────────┤          │
     ▲         success in half-open              │ wait    │
     │                                           │ Break   │
     │         ┌──────────┐                     │ Duration│
     └─────────┤HALF-OPEN │<─────────────────────┤          │
               │          │  BreakDuration      │          │
               │ test req │  expired            └──────────┘
               └──────────┘
   |
   +-- failure ----> OPEN
```

### States

- `CLOSED` — normal operation
- `OPEN` — calls blocked
- `HALF-OPEN` — a test call is allowed

---

## Sliding Window

Polly uses a time window to calculate the failure rate.

```text
Sampling Duration = 10 seconds

SUCCESS SUCCESS SUCCESS FAILURE FAILURE SUCCESS FAILURE FAILURE FAILURE SUCCESS

Total:   10 calls
Failures:   5
FailureRatio = 50%
```

The circuit opens when:

```text
FailureRatio >= configured value
AND
Throughput >= MinimumThroughput
```

---

## Adaptive Traffic Control

The adaptive layer (`CircuitBreaker.Adaptive`) operates before the Circuit Breaker,
providing traffic control and additional protection.

```text
Telemetry
   |
   v
Health Score Calculator
   |
   v
Adaptive Traffic Controller
   ├── Rate Limiting
   ├── Concurrency Control
   ├── Request Shedding
   v
Circuit Breaker
   |
   v
ICircuitBreakerSnapshotSource ──> Background Export ──> Exporters
```

| Score | State |
|------:|--------|
| 1.0   | Healthy |
| 0.8   | Mild degradation |
| 0.5   | Moderate degradation |
| 0.2   | Critical state |
| 0.0   | Severe failure |

---

## Repository Structure

```
src/
  CircuitBreaker.Core/
  CircuitBreaker.Telemetry/
    DependencyInjection/
      ServiceCollectionExtensions.cs
    CircuitBreakerSnapshot.cs
    ICircuitBreakerSnapshotSource.cs
    ICircuitBreakerMetricsExporter.cs
    ICircuitBreakerTelemetryPublisher.cs
    CircuitBreakerTelemetryPublisher.cs
    CircuitBreakerTelemetryBackgroundService.cs
  CircuitBreaker.Adaptive/
    DependencyInjection/
  CircuitBreaker.Sample/
  CircuitBreaker.Adaptive.Sample/
  CircuitBreaker.Tests/
README.md
README.txt
TUNING_GUIDE.md
TUNING_GUIDE_EN.md
```

---

## Main Dependencies

- `Polly` 8.6.6
- `Microsoft.Extensions.DependencyInjection` 9.0.0
- `Microsoft.Extensions.Hosting.Abstractions` 9.0.0
- `Microsoft.Extensions.Logging.Abstractions` 9.0.0

---

## Contributing

Contributions welcome! Please ensure:
1. All tests pass (`dotnet test`)
2. No compiler warnings
3. Code follows C# conventions
4. Update tests for new features

---

## License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## Support

- 📖 **Documentation**: [INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md) | [TUNING_GUIDE_EN.md](TUNING_GUIDE_EN.md) | [TUNING_GUIDE.md](TUNING_GUIDE.md)
- 🐛 **Issues**: Report via GitHub Issues
- 💬 **Discussions**: Use GitHub Discussions for questions
- 📧 **Email**: agsilveira.7@gmail.com

---

**Last Updated**: June 2026  
**Version**: 0.5.0  
**Status**: Production Ready
