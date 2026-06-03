# CircuitBreaker - Production-Ready Resilience Library

> .NET 10 library that wraps Polly v8's **Advanced Circuit Breaker** in a simple, consistent API with adaptive traffic control, comprehensive telemetry, and production-grade reliability.

**License**: GNU General Public License v3.0  
**Status**: ✅ Production Ready | 28/28 Tests Passing | Optimized for High-Throughput

---

## Overview

This repository contains a battle-tested abstraction layer over Polly's Circuit Breaker with:

- ✅ **Simplified API** - Clean, intuitive interface
- ✅ **Async/Await Native** - Full `CancellationToken` support
- ✅ **Telemetry-First** - Real-time metrics collection (O(k) optimized)
- ✅ **Adaptive Control** - Dynamic rate limiting & concurrency management
- ✅ **Thread-Safe** - Atomic operations, no race conditions
- ✅ **DI Compatible** - Microsoft.Extensions.DependencyInjection ready
- ✅ **Well-Tested** - 28 unit tests with 100% pass rate
- ✅ **Production-Documented** - Complete tuning guide included

### Included Projects

| Project | Purpose | Status |
|---------|---------|--------|
| `CircuitBreaker.Core` | Polly v8 wrapper + state management | ✅ |
| `CircuitBreaker.Telemetry` | Time-bucketed metrics (O(k) optimized) | ✅ |
| `CircuitBreaker.Adaptive` | Adaptive rate limiting + concurrency | ✅ |
| `CircuitBreaker.Sample` | Core usage example | ✅ |
| `CircuitBreaker.Adaptive.Sample` | Advanced adaptive demo | ✅ |
| `CircuitBreaker.Tests` | 28 unit tests (NEW) | ✅ |

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

### 4. Run Examples

```bash
# Build all projects
dotnet build src/CircuitBreaker.slnx

# Run core sample
dotnet run --project src/CircuitBreaker.Sample

# Run adaptive sample  
dotnet run --project src/CircuitBreaker.Adaptive.Sample

# Run tests
dotnet test src/CircuitBreaker.Tests
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

ICircuitBreaker
      |
      v

CircuitBreaker
  (wrapper)
      |
      v

ResiliencePipeline
   (Polly v8)
```

The `CircuitBreaker` acts as a thin wrapper over Polly's `ResiliencePipeline`, adding comprehensive telemetry and adaptive control layers.

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

- **.NET 10.0** or later
- **Polly 8.6.6+** (included as dependency)
- Optional: Microsoft.Extensions.DependencyInjection for DI integration
- Optional: Microsoft.Extensions.Logging for structured logging

---

## Testing

```bash
# Run all tests with verbose output
dotnet test src/CircuitBreaker.Tests -v normal

# Run with coverage
dotnet test src/CircuitBreaker.Tests --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test src/CircuitBreaker.Tests --filter "FullyQualifiedName~CircuitBreakerTests"
```

**Test Results**: 28/28 passing (100% pass rate)  
**Test Categories**:
- Circuit Breaker state transitions & callbacks
- Telemetry collection & aggregation  
- Concurrent operations (race condition verification)
- Health score calculation
- Adaptive rate limiting & concurrency

---

## Contributing

Contributions welcome! Please ensure:
1. All tests pass (`dotnet test`)
2. No compiler warnings
3. Code follows C# conventions
4. Update tests for new features

---

## License

This project is licensed under the **GNU General Public License v3.0** - see [LICENSE](LICENSE) file for details.

### You are free to:
- ✅ Use commercially
- ✅ Modify the source code
- ✅ Distribute
- ✅ Use privately

### Under the condition that you:
- 📋 Disclose source
- 📋 State changes
- 📋 Use same license (GPLv3)

For proprietary use, contact the maintainer for alternative licensing.

---

## Support

- 📖 **Documentation**: [TUNING_GUIDE.md](TUNING_GUIDE.md)
- 🐛 **Issues**: Report via GitHub Issues
- 💬 **Discussions**: Use GitHub Discussions for questions
- 📧 **Email**: [Add contact info]

---

**Last Updated**: June 2, 2026  
**Version**: 1.0.0  
**Status**: ✅ Production Ready

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
```

### Concept

Instead of relying only on discrete states:

```text
Closed → Open → Half-Open
```

you can continuously calculate a health indicator:

```text
Health Score = 0.0 .. 1.0
```

| Score | State |
|------:|--------|
| 1.0   | Healthy |
| 0.8   | Mild degradation |
| 0.5   | Moderate degradation |
| 0.2   | Critical state |
| 0.0   | Severe failure |

---

## Repository structure

- `src/CircuitBreaker.Core`
- `src/CircuitBreaker.Telemetry`
- `src/CircuitBreaker.Adaptive`
- `src/CircuitBreaker.Sample`
- `src/CircuitBreaker.Adaptive.Sample`
- `dist/` (possible build or package output)

---

## Main dependencies

- `Polly` 8.6.6
- `Microsoft.Extensions.DependencyInjection` 10.0.8

---

## License

This project is licensed under the **GNU General Public License v3.0 (GPL-3.0)**.
See the [LICENSE](LICENSE) file for the full text. Replace the copyright holder and year
in `LICENSE` with the appropriate values for your project if desired.

---

## Support

For questions or support, contact: agsilveira.7@gmail.com


---

## Reference

See also `README.txt` for a complementary overview and additional examples.
