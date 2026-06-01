===============================================================================
CIRCUITBREAKER
===============================================================================

.NET 10 library that wraps Polly v8's Advanced Circuit Breaker in a simple,
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

dist/        (possible build or package output)
README.md
README.txt

-------------------------------------------------------------------------------
PROJECTS IN THE SOLUTION
-------------------------------------------------------------------------------

CircuitBreaker.slnx includes:

  * CircuitBreaker.Core
  * CircuitBreaker.Telemetry
  * CircuitBreaker.Adaptive
  * CircuitBreaker.Sample
  * CircuitBreaker.Adaptive.Sample

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
  * Microsoft.Extensions.DependencyInjection 10.0.8

-------------------------------------------------------------------------------
LICENSE
-------------------------------------------------------------------------------

Project distributed for educational and demonstration purposes.

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


Solution:
    Atomic state machine.

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
  * Microsoft.Extensions.DependencyInjection 10.0.8

-------------------------------------------------------------------------------
LICENSE
-------------------------------------------------------------------------------

Project distributed for educational and demonstration purposes.

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

Why Wrapper?

    * Simplified API
    * Lower coupling
    * Easier future engine replacement
    * Centralized configuration
    * Own state tracking

Why volatile int?

    * Lock-free
    * Atomic read
    * Thread-safe
    * Low overhead

-------------------------------------------------------------------------------
FUTURE EVOLUTION
-------------------------------------------------------------------------------

Possible evolution toward an adaptive traffic control system based on:

    * Error Rate
    * Throughput
    * Latency
    * P95
    * P99
    * Timeouts
    * Resource saturation

Concept:

    Health Score = 0.0 .. 1.0

Possible future actions:

    * Rate Limiting
    * Concurrency Control
    * Request Shedding
    * Circuit Breaker

In this architecture, the Circuit Breaker becomes the last layer of protection.

-------------------------------------------------------------------------------
DEPENDENCIES
-------------------------------------------------------------------------------

Polly
    Version 8.6.6

Microsoft.Extensions.DependencyInjection
    Version 10.0.8

-------------------------------------------------------------------------------
LICENSE
-------------------------------------------------------------------------------

Project distributed for educational and demonstration purposes.

===============================================================================
EOF
===============================================================================