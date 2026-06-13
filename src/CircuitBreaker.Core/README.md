# CircuitBreaker.Core

Production-ready wrapper over [Polly v8](https://www.pollydocs.org/) Advanced Circuit Breaker.

## Install

```bash
dotnet add package CircuitBreaker.Core
```

## Quick start

```csharp
using CircuitBreaker.Core;
using Polly.CircuitBreaker;

var breaker = CircuitBreakerFactory.Create(
    new CircuitBreakerOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 8,
        SamplingDuration = TimeSpan.FromSeconds(10),
        BreakDuration = TimeSpan.FromSeconds(5),
        OnOpened = d => Console.WriteLine($"[{breaker.ResourceName}] opened for {d.TotalSeconds}s"),
        OnClosed = () => Console.WriteLine($"[{breaker.ResourceName}] closed"),
    },
    resourceName: "PaymentAPI");

try
{
    await breaker.ExecuteAsync(() => CallDownstreamAsync());
}
catch (BrokenCircuitException)
{
    // fallback
}
```

See the [repository](https://github.com/rootmese/circuit_breaker) for tuning guides and adaptive traffic control.
