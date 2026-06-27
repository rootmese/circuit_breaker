===============================================================================
CIRCUITBREAKER
===============================================================================

.NET library that wraps Polly v8's Advanced Circuit Breaker in a simple,
thread-safe API ready for NuGet distribution, with a pluggable telemetry
export pipeline.

-------------------------------------------------------------------------------
OVERVIEW
-------------------------------------------------------------------------------

This repository provides:

  * `CircuitBreaker.Core` — wrapper for Polly's Circuit Breaker with a
    simplified API and Dependency Injection integration.
  * `CircuitBreaker.Telemetry` — sliding window metrics provider + pluggable
    export pipeline (snapshot, publisher, background service, exporter contracts).
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
  * Pluggable Telemetry Export (Prometheus, OTel, Zabbix, custom)
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

5. Run tests:

       dotnet test src/CircuitBreaker.slnx

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

    AdaptiveCircuitBreakerDecorator  <-- ICircuitBreakerSnapshotSource
          |                                        |
          v                               CircuitBreakerTelemetryBackgroundService
    ICircuitBreaker                               |
          |                               ICircuitBreakerTelemetryPublisher
          v                                       |
    ResiliencePipeline         +------------------+------------------+
       (Polly v8)          ExporterA          ExporterB          ExporterN ...

The `CircuitBreaker` acts as a thin wrapper over Polly's `ResiliencePipeline`.
The state machine is delegated to Polly:

    CLOSED -> OPEN -> HALF-OPEN -> CLOSED

The telemetry export pipeline is fully decoupled: the breaker does not know
about exporters; it only implements ICircuitBreakerSnapshotSource.

-------------------------------------------------------------------------------
REPOSITORY STRUCTURE
-------------------------------------------------------------------------------

src/
  CircuitBreaker.Core/
  CircuitBreaker.Telemetry/
    DependencyInjection/
      ServiceCollectionExtensions.cs    AddCircuitBreakerTelemetry()
                                        AddCircuitBreakerExporter<T>()
    CircuitBreakerSnapshot.cs           Immutable observability record
    ICircuitBreakerSnapshotSource.cs    Decoupling abstraction
    ICircuitBreakerMetricsExporter.cs   Implement to push metrics anywhere
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
INTEGRATION_GUIDE.md
TUNING_GUIDE.md
TUNING_GUIDE_EN.md

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
TELEMETRY EXPORT PIPELINE (NEW IN v0.5.0)
-------------------------------------------------------------------------------

The export pipeline lets you push live circuit breaker metrics to any external
system without coupling the breaker itself to any monitoring tool.

Types:

  CircuitBreakerSnapshot (sealed record)
      ResourceName, InstanceId, State, ErrorRate, LatencyMs, P99LatencyMs,
      Throughput, TimeoutRate, ResourceSaturation, ActiveConnections,
      HealthScore, Timestamp (DateTimeOffset UTC)

  ICircuitBreakerSnapshotSource
      Implemented by AdaptiveCircuitBreakerDecorator.
      Provides GetSnapshotAsync() without creating a circular dependency
      between CircuitBreaker.Telemetry and CircuitBreaker.Adaptive.

  ICircuitBreakerMetricsExporter
      Implement this interface to push snapshots to any sink:
          Prometheus, OpenTelemetry, Zabbix, InfluxDB, dashboards, etc.

  ICircuitBreakerTelemetryPublisher / CircuitBreakerTelemetryPublisher
      Fans out to all registered exporters in parallel.
      A failure in one exporter does not affect the others.

  CircuitBreakerTelemetryBackgroundService (BackgroundService)
      Collects snapshots on a configurable interval and publishes them.

DI registration example:

    // 1. Register the adaptive breaker (also registers ICircuitBreakerSnapshotSource)
    services.AddAdaptiveCircuitBreaker(circuitOptions, adaptiveOptions, "PaymentAPI");

    // 2. Register exporter(s)
    services.AddCircuitBreakerExporter<MyPrometheusExporter>();

    // 3. Start background export
    services.AddCircuitBreakerTelemetry(exportInterval: TimeSpan.FromSeconds(10));

Custom exporter:

    public class MyPrometheusExporter : ICircuitBreakerMetricsExporter
    {
        public Task ExportAsync(CircuitBreakerSnapshot s, CancellationToken ct)
        {
            // use s.ResourceName, s.HealthScore, s.ErrorRate, s.State, etc.
            return Task.CompletedTask;
        }
    }

Planned exporters (community / future packages):

  * CircuitBreaker.Prometheus
  * CircuitBreaker.OpenTelemetry
  * CircuitBreaker.Zabbix

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
    Strict try-finally blocks guaranteeing concurrency lock release and
    telemetry recording regardless of exceptions.

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
    ResourceName

AdaptiveCircuitBreakerDecorator (also implements ICircuitBreakerSnapshotSource)

    GetSnapshotAsync()          Returns a CircuitBreakerSnapshot
    GetLatestTelemetryAsync()   Returns raw TelemetrySnapshot
    CurrentHealthScore          Live HealthScore (0.0-1.0)

-------------------------------------------------------------------------------
MAIN DEPENDENCIES
-------------------------------------------------------------------------------

  * Polly 8.6.6
  * Microsoft.Extensions.DependencyInjection 9.0.0
  * Microsoft.Extensions.Hosting.Abstractions 9.0.0
  * Microsoft.Extensions.Logging.Abstractions 9.0.0

-------------------------------------------------------------------------------
LICENSE
-------------------------------------------------------------------------------

Licensed under the MIT License.

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

When used correctly, cancellations triggered by Polly also cancel the user's
operation.

-------------------------------------------------------------------------------
OBSERVABILITY
-------------------------------------------------------------------------------

The library does not generate logs automatically.

The consumer defines callbacks:

    OnOpened
    OnClosed
    OnHalfOpened

For structured metrics export, use the telemetry pipeline:

    ICircuitBreakerMetricsExporter  (implement and register via DI)
    AddCircuitBreakerTelemetry()    (registers publisher + background service)
    AddCircuitBreakerExporter<T>()  (registers a concrete exporter)

This avoids side effects and keeps the library quiet by default.

-------------------------------------------------------------------------------
DEPENDENCY INJECTION
-------------------------------------------------------------------------------

Recommended to register as a Singleton.

Simple example:

    services.AddSingleton<ICircuitBreaker>(...)

Adaptive with telemetry export:

    services.AddAdaptiveCircuitBreaker(circuitOptions, adaptiveOptions, "SvcName");
    services.AddCircuitBreakerExporter<MyExporter>();
    services.AddCircuitBreakerTelemetry(TimeSpan.FromSeconds(10));

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

Why ICircuitBreakerSnapshotSource?

    Without this interface, CircuitBreaker.Telemetry would need to reference
    CircuitBreaker.Adaptive, creating a circular dependency.
    The interface lives in Telemetry; the Adaptive package implements it.
    This keeps both packages independently publishable on NuGet.

Why CircuitBreakerSnapshot as a sealed record?

    Immutability guarantees thread-safety across the publisher fan-out.
    DateTimeOffset (not DateTime) avoids timezone ambiguity.
    InstanceId (Guid) allows distinguishing multiple breakers for the same
    resource in multi-instance deployments.


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
  The CircuitBreaker introduces less than 0.5 us of overhead per call,
  making it negligible for most I/O-bound workloads such as HTTP, gRPC,
  database access, message brokers, and external service integrations.

-------------------------------------------------------------------------------
 VERSIONING POLICY
-------------------------------------------------------------------------------

This project follows a maintenance-oriented versioning model.

Version numbers do not primarily represent technical stability.
Instead, they represent the maintainer's support commitment.

0.x Releases
------------

Versions in the 0.x series are considered functional and tested.

A 0.x release may be used in production environments at the user's
discretion, but:

  - APIs may change without notice
  - Features may be redesigned
  - Long-term maintenance is not guaranteed
  - Backward compatibility is not a goal

The purpose of the 0.x series is to validate architecture,
collect feedback, and evaluate community adoption.

1.x Releases
------------

A project reaches version 1.x only when there is an explicit
commitment to ongoing maintenance.

This includes:

  - Active updates
  - Dependency modernization
  - Security fixes when required
  - Backward compatibility considerations
  - Long-term roadmap commitment

In this model, version 1.x represents a maintenance commitment
rather than a statement about technical maturity.

Notes
-----

A 0.x release should not be interpreted as unstable software.

Many projects in the 0.x series may already be suitable for
production workloads, depending on the user's requirements.

The distinction between 0.x and 1.x is support commitment,
not implementation quality.

===============================================================================
EOF
===============================================================================
