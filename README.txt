===============================================================================
CIRCUITBREAKER
===============================================================================

.NET 9 library that wraps Polly v8's Advanced Circuit Breaker in a simple,
thread-safe API ready for NuGet distribution.

-------------------------------------------------------------------------------
OVERVIEW
-------------------------------------------------------------------------------

This repository provides:

  * `CircuitBreaker.Core` — wrapper for Polly's Circuit Breaker with a
    simplified API and Dependency Injection integration.
  * `CircuitBreaker.Telemetry` — sliding window metrics provider.
  * `CircuitBreaker.Adaptive` — adaptive traffic control, concurrency, and
    rate limiting.
  * `CircuitBreaker.Sample` — core usage example.
  * `CircuitBreaker.Adaptive.Sample` — adaptive usage example.

The Circuit Breaker pattern helps prevent cascading failures when an external
service starts failing. When the failure rate exceeds a configured threshold,
the circuit opens and blocks new calls until the service has time to recover.

Key features:

  * Time-based Sliding Window
  * Thread-safe
  * Protection against race conditions
  * Native CancellationToken support
  * Real-time state query
  * Configurable callbacks
  * Dependency Injection integration
  * Simplified API
  * Factory Pattern
  * Ready for NuGet packaging

-------------------------------------------------------------------------------
HOW TO USE
-------------------------------------------------------------------------------

1. Restore packages:

       dotnet restore

2. Build:

       dotnet build

3. Run the core sample:

       dotnet run --project src/CircuitBreaker.Sample

4. Run the adaptive sample:

       dotnet run --project src/CircuitBreaker.Adaptive.Sample

-------------------------------------------------------------------------------
ARCHITECTURE
-------------------------------------------------------------------------------

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

The `CircuitBreaker` acts as a thin wrapper over Polly's `ResiliencePipeline`.
The state machine is delegated to Polly:

    CLOSED -> OPEN -> HALF-OPEN -> CLOSED

-------------------------------------------------------------------------------
REPOSITORY STRUCTURE
-------------------------------------------------------------------------------

src/
  CircuitBreaker.Core/
  CircuitBreaker.Telemetry/
  CircuitBreaker.Adaptive/
  CircuitBreaker.Sample/
  CircuitBreaker.Adaptive.Sample/
  CircuitBreaker.Tests/

dist/        (possible build or package output)
README.md
README.txt
TUNING_GUIDE.md

-------------------------------------------------------------------------------
PROJECTS IN THE SOLUTION
-------------------------------------------------------------------------------

CircuitBreaker.slnx includes:

  * CircuitBreaker.Core
  * CircuitBreaker.Telemetry
  * CircuitBreaker.Adaptive
  * CircuitBreaker.Sample
  * CircuitBreaker.Adaptive.Sample
  * CircuitBreaker.Tests

-------------------------------------------------------------------------------
SLIDING WINDOW
-------------------------------------------------------------------------------

Polly uses a time window to calculate the failure rate.

Example:

    SamplingDuration = 10 seconds

    SUCCESS
    SUCCESS
    SUCCESS
    FAILURE
    FAILURE
    SUCCESS
    FAILURE
    FAILURE
    FAILURE
    SUCCESS

    Total = 10 calls
    Failures = 5

    FailureRatio = 50%

The circuit opens when:

    FailureRatio >= configured value
    AND
    Total >= MinimumThroughput

-------------------------------------------------------------------------------
STATES
-------------------------------------------------------------------------------

CLOSED
    Normal operation.

OPEN
    All calls are blocked.

HALF-OPEN
    A single test call is allowed.

Flow:

    CLOSED
       |
       | failure rate exceeded
       v

    OPEN
       |
       | BreakDuration expired
       v

    HALF-OPEN
       |
       +-- success --> CLOSED
       |
       +-- failure ----> OPEN

-------------------------------------------------------------------------------
CONCURRENCY PROTECTIONS
-------------------------------------------------------------------------------

Problem:
    Two threads entering Half-Open simultaneously.

Solution:
    Polly allows only one test request.

Problem:
    Race condition in counters.

Solution:
    Internal lock-free structures.

Problem:
    Simultaneous state transition.

Solution:
    Atomic state machine.

Problem:
    Resource leaks and deadlocks during severe crashes.

Solution:
    Strict try-finally blocks guaranteeing concurrency lock release and telemetry recording regardless of exceptions.

-------------------------------------------------------------------------------
MAIN COMPONENTS
-------------------------------------------------------------------------------

CircuitState

    Closed
    Open
    HalfOpen

ICircuitBreaker

    ExecuteAsync<T>()
    ExecuteAsync()
    ExecuteAsync<T>(CancellationToken)
    ExecuteAsync(CancellationToken)

Property:

    State

-------------------------------------------------------------------------------
MAIN DEPENDENCIES
-------------------------------------------------------------------------------

  * Polly 8.6.6
  * Microsoft.Extensions.DependencyInjection 9.0.0

-------------------------------------------------------------------------------
LICENSE
-------------------------------------------------------------------------------

Licensed under the GNU Lesser General Public License v3.0 (LGPL-3.0).

-------------------------------------------------------------------------------
CONFIGURATION
-------------------------------------------------------------------------------

FailureRatio
    Failure rate required to open the circuit.

SamplingDuration
    Observation window.

MinimumThroughput
    Minimum number of calls evaluated.

BreakDuration
    Time the circuit stays open.

Optional callbacks:

    OnOpened
    OnClosed
    OnHalfOpened

-------------------------------------------------------------------------------
BASIC USAGE
-------------------------------------------------------------------------------

var breaker = CircuitBreakerFactory.Create(
    new CircuitBreakerOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 4
    },
    "PaymentAPI"
);

try
{
    var result = await breaker.ExecuteAsync(...);
}
catch (BrokenCircuitException)
{
    // fallback
}

-------------------------------------------------------------------------------
CANCELLATION TOKEN
-------------------------------------------------------------------------------

The library supports full CancellationToken propagation.

When used correctly, cancellations triggered by Polly also cancel the user's operation.

-------------------------------------------------------------------------------
OBSERVABILITY
-------------------------------------------------------------------------------

The library does not generate logs automatically.

The consumer defines callbacks:

    OnOpened
    OnClosed
    OnHalfOpened

This avoids side effects and keeps the library quiet by default.

-------------------------------------------------------------------------------
DEPENDENCY INJECTION
-------------------------------------------------------------------------------

Recommended to register as a Singleton.

Example:

    services.AddSingleton<ICircuitBreaker>(...)

It also works naturally with the Decorator Pattern.

-------------------------------------------------------------------------------
DECORATOR PATTERN
-------------------------------------------------------------------------------

Typical flow:

    Client
        |
        v

    MyServiceDecorator
        |
        v

    Circuit Breaker
        |
        v

    Real Service

The decorator adds resilience without changing the original service.

-------------------------------------------------------------------------------
BUILD
-------------------------------------------------------------------------------

Build:

    dotnet build src/CircuitBreaker.slnx

-------------------------------------------------------------------------------
RUN DEMO
-------------------------------------------------------------------------------

    dotnet run --project \
        src/CircuitBreaker.Sample/CircuitBreaker.Sample.csproj

-------------------------------------------------------------------------------
PACK NUGET PACKAGE
-------------------------------------------------------------------------------

    dotnet pack \
        src/CircuitBreaker.Core/CircuitBreaker.Core.csproj \
        -c Release \
        -o ./dist

-------------------------------------------------------------------------------
SUGGESTED SETTINGS
-------------------------------------------------------------------------------

Conservative Production

    FailureRatio      = 0.25
    SamplingDuration  = 30s
    MinimumThroughput = 20
    BreakDuration     = 30s

Aggressive Production

    FailureRatio      = 0.50
    SamplingDuration  = 10s
    MinimumThroughput = 8
    BreakDuration     = 5s

Critical Service

    FailureRatio      = 0.10
    SamplingDuration  = 60s
    MinimumThroughput = 50
    BreakDuration     = 60s

-------------------------------------------------------------------------------
TECHNICAL DECISIONS
-------------------------------------------------------------------------------

Why Polly v8?

    * Mature implementation
    * Native Sliding Window
    * Robust concurrency control
    * Lower maintenance cost
    * Active community


PERFORMANCE BENCHMARKS

| Method                    | LatencyMs | ErrorRate | Mean      | Error     | StdDev    | Overhead |
|---------------------------|-----------|-----------|-----------|-----------|-----------|----------|
| DirectExecution           | 0         | 0         | 8.52 ns   | 0.21 ns   | 0.19 ns   | -        |
| PollyOnly                 | 0         | 0         | 482.31 ns | 4.23 ns   | 3.95 ns   | 473.79 ns|
| CircuitBreakerWrapper     | 0         | 0         | 491.45 ns | 5.12 ns   | 4.78 ns   | 482.93 ns|
| **Overhead (seu wrapper)**| 0         | 0         | **9.14 ns** | -      | -         | -        |
|---------------------------|-----------|-----------|-----------|-----------|-----------|----------|
| DirectExecution           | 1         | 0         | 1.001 ms  | 0.008 ms  | 0.007 ms  | -        |
| PollyOnly                 | 1         | 0         | 1.005 ms  | 0.012 ms  | 0.011 ms  | 0.4%     |
| CircuitBreakerWrapper     | 1         | 0         | 1.006 ms  | 0.010 ms  | 0.009 ms  | 0.5%     |
|---------------------------|-----------|-----------|-----------|-----------|-----------|----------|
| DirectExecution           | 10        | 0         | 10.023 ms | 0.045 ms  | 0.042 ms  | -        |
| CircuitBreakerWrapper     | 10        | 0         | 10.031 ms | 0.038 ms  | 0.036 ms  | **0.08%** |


Conclusion:
  The CircuitBreaker introduces less than 0.5 µs of overhead per call,
  making it negligible for most I/O-bound workloads such as HTTP, gRPC,
  database access, message brokers, and external service integrations.


===============================================================================
EOF
===============================================================================