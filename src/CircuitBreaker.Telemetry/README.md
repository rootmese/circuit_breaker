# CircuitBreaker.Telemetry

Thread-safe rolling-window telemetry for circuit breaker health scoring.

## Install

```bash
dotnet add package CircuitBreaker.Telemetry
```

Collects error rate, latency, P99, throughput, timeout rate, and resource saturation over a configurable time window.

Used by `CircuitBreaker.Adaptive` and can be consumed directly for custom health dashboards.

See the [repository](https://github.com/rootmese/circuit_breaker) for details.
