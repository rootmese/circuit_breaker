# CircuitBreaker.Adaptive

Adaptive rate limiting and concurrency control layered on top of `CircuitBreaker.Core`.

## Install

```bash
dotnet add package CircuitBreaker.Adaptive
```

## Quick start

```csharp
using CircuitBreaker.Adaptive;
using CircuitBreaker.Adaptive.DependencyInjection;
using CircuitBreaker.Core;

services.AddAdaptiveCircuitBreaker(
    circuitOptions: new CircuitBreakerOptions { /* ... */ },
    adaptiveOptions: new AdaptiveTrafficControlOptions
    {
        InitialMaxRequestsPerSecond = 200,
        InitialMaxConcurrency = 20,
    },
    resourceName: "PaymentAPI");
```

Handle shedding exceptions separately from `BrokenCircuitException`:

- `RateLimitExceededException`
- `ConcurrencyLimitExceededException`

See the [repository](https://github.com/rootmese/circuit_breaker) for tuning guides.
