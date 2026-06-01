# CircuitBreaker

> .NET 10 library that wraps Polly v8's **Advanced Circuit Breaker** in a simple, consistent API ready for NuGet distribution.

---

## Overview

This repository contains an abstraction layer over Polly's Circuit Breaker, focusing on:

- simplified API
- integration with `CancellationToken`
- telemetry metrics
- adaptive traffic control
- Dependency Injection compatibility

### Included projects

- `CircuitBreaker.Core` — Polly wrapper with Circuit Breaker and factory
- `CircuitBreaker.Telemetry` — metrics provider and sliding window
- `CircuitBreaker.Adaptive` — adaptive traffic control, rate limiting, and concurrency
- `CircuitBreaker.Sample` — core usage sample
- `CircuitBreaker.Adaptive.Sample` — adaptive usage sample

---

## How to use

1. Restore packages:

```bash
dotnet restore
```

2. Build all projects:

```bash
dotnet build
```

3. Run the core sample:

```bash
dotnet run --project src/CircuitBreaker.Sample
```

4. Run the adaptive sample:

```bash
dotnet run --project src/CircuitBreaker.Adaptive.Sample
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

The `CircuitBreaker` acts as a thin wrapper over Polly's `ResiliencePipeline`.

---

## State Machine

```text
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

Project distributed for educational and demonstration purposes.

---

## Reference

See also `README.txt` for a complementary overview and additional examples.
