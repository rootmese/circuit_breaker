# Circuit Breaker – Tuning Guide & Best Practices

## 📋 Index
1. [Fundamental Concepts](#fundamental-concepts)
2. [Circuit Breaker Configuration](#circuit-breaker-configuration)
3. [Adaptive Tuning](#adaptive-tuning)
4. [Health Score](#health-score)
5. [Troubleshooting](#troubleshooting)
6. [Production Checklist](#production-checklist)

---

## Fundamental Concepts

### Circuit Breaker States

The circuit breaker has three states:

| State          | Behavior                               | Transition                                               |
|----------------|----------------------------------------|----------------------------------------------------------|
| **CLOSED**     | Requests flow normally                 | → **OPEN** when failure ratio exceeds threshold          |
| **OPEN**       | All requests are blocked               | → **HALF‑OPEN** after `BreakDuration`                   |
| **HALF‑OPEN**  | One test request is allowed            | → **CLOSED** if it succeeds, **OPEN** if it fails        |

### Core Parameters

- **FailureRatio** – Failure rate that triggers opening (0.5 = 50 %).
- **MinimumThroughput** – Minimum number of calls needed to evaluate the ratio.
- **SamplingDuration** – Time window for monitoring.
- **BreakDuration** – How long the circuit stays open.

---

## Circuit Breaker Configuration

### Default Configuration (Recommended for APIs)

```csharp
var breaker = CircuitBreakerFactory.Create(
    new CircuitBreakerOptions
    {
        FailureRatio      = 0.5,                      // 50 % failures
        MinimumThroughput = 8,                        // at least 8 calls
        SamplingDuration  = TimeSpan.FromSeconds(10),
        BreakDuration     = TimeSpan.FromSeconds(5),

        OnOpened   = duration =>
            logger.LogWarning($"Circuit opened for {duration.TotalSeconds}s"),
        OnClosed   = () =>
            logger.LogInformation("Circuit closed - service recovered"),
        OnHalfOpened = () =>
            logger.LogInformation("Circuit half-open - testing service")
    },
    resourceName: "PaymentAPI"
);
```

### Recommended Profiles

#### 🟢 **Resilient (Default)**
For reliable external APIs, background jobs:
```
FailureRatio:      0.5 (50 %)
MinimumThroughput: 8
SamplingDuration: 10 s
BreakDuration:     5 s
```

#### 🟡 **Moderate (Internal Services)**
For unstable or legacy micro‑services:
```
FailureRatio:      0.3 (30 %)
MinimumThroughput: 5
SamplingDuration:  5 s
BreakDuration:    10 s
```

#### 🔴 **Aggressive (Critical)**
For mission‑critical APIs:
```
FailureRatio:      0.1 (10 %)
MinimumThroughput: 2
SamplingDuration:  3 s
BreakDuration:    30 s
```

### Minimum Throughput Calculation

**Formula:** `MinimumThroughput = (ExpectedRPS * SamplingDuration.TotalSeconds) / 4`

**Example:** For 100 RPS expected
- `SamplingDuration = 10 s`
- `MinimumThroughput = (100 × 10) / 4 = **250 calls**`

---

## Adaptive Tuning

### Basic Usage

```csharp
services.AddAdaptiveCircuitBreaker(
    circuitOptions: new CircuitBreakerOptions { /* ... */ },
    adaptiveOptions: new AdaptiveTrafficControlOptions
    {
        InitialMaxRequestsPerSecond = 1000,
        InitialMaxConcurrency       = 100,
        ControlLoopInterval         = TimeSpan.FromMilliseconds(100),
        TelemetryWindow             = TimeSpan.FromSeconds(30)
    }
);
```

### Adaptive Control Maps

The system maps **Health Score** to traffic limits:

**Health Score → Rate Limit**
- `1.0` (Healthy) → 100 % of permits
- `0.8` → 90 %
- `0.5` (Degraded) → 60 %
- `0.2` (Critical) → 10 %
- `< 0.1` → Blocked

**Health Score → Concurrency**
- `1.0` → 100 % of max concurrency
- `0.5` → 50 %
- `< 0.1` → Allow only 1 concurrent request

### Fine‑Tuning Initial Limits

```csharp
// For high‑throughput systems
var options = new AdaptiveTrafficControlOptions
{
    InitialMaxRequestsPerSecond = 5000,  // adjust to expected RPS
    InitialMaxConcurrency       = 500,   // increase if connections are expensive
    ControlLoopInterval         = TimeSpan.FromMilliseconds(50),  // more sensitive
    ScoreSmoothingFactor        = 0.3    // 0.0‑1.0: how quickly it reacts
};
```

---

## Health Score

### Components

The health score is a weighted average of six metrics:

| Metric               | Weight | Healthy Threshold | Warning       | Critical      |
|----------------------|--------|-------------------|---------------|---------------|
| **ErrorRate**        | 35 %   | < 5 %             | < 10 %        | < 25 %        |
| **Latency**          | 20 %   | < 100 ms          | < 200 ms      | < 500 ms      |
| **P99Latency**       | 15 %   | < 200 ms          | < 400 ms      | < 800 ms      |
| **TimeoutRate**      | 10 %   | < 2 %             | < 5 %         | < 10 %        |
| **ResourceSat.**     | 5 %    | < 30 %            | < 60 %        | < 80 %        |
| **Throughput**       | 15 %   | > 1000 req/s      | > 500 req/s   | > 100 req/s   |

### Customizing Thresholds

```csharp
var calculator = new HealthScoreCalculator();

// Change error‑rate thresholds for a lenient service
calculator.ConfigureThreshold(
    metric: "ErrorRate",
    healthy: 0.1,    // 10 % is healthy for this service
    warning: 0.2,
    critical: 0.5
);

// Increase latency weight if latency is critical
// (requires recompilation – alternatively use external config)
```

### Health Score Diagnostics

```csharp
var adaptive = provider.GetRequiredService<AdaptiveCircuitBreakerDecorator>();
var telemetry = await adaptive.GetLatestTelemetryAsync();

Console.WriteLine($"""
    Health Score: {adaptive.CurrentHealthScore}
    Error Rate: {telemetry.ErrorRate:P}
    P99 Latency: {telemetry.P99LatencyMs:F0}ms
    Throughput: {telemetry.Throughput} req/s
    Status: {(adaptive.CurrentHealthScore.IsHealthy ? "HEALTHY" : "DEGRADED")}
    """);
```

---

## Troubleshooting

### ❌ "Circuit opens too often"

**Possible causes:**
- FailureRatio too low (e.g., 0.1)
- MinimumThroughput too low
- Timeout limits too short

**Fix:**
```csharp
new CircuitBreakerOptions
{
    FailureRatio      = 0.5,          // ↑ from 0.1 to 0.5
    MinimumThroughput = 10,           // ↑ from 2 to 10
    BreakDuration     = TimeSpan.FromSeconds(10)  // longer recovery window
}
```

### ❌ "Circuit never opens (cascading failures)"

**Possible causes:**
- FailureRatio too high (e.g., 0.9)
- SamplingDuration too long
- Errors not being logged

**Fix:**
```csharp
new CircuitBreakerOptions
{
    FailureRatio      = 0.3,                              // ↓ from 0.9 to 0.3
    SamplingDuration  = TimeSpan.FromSeconds(3),         // ↓ from 30 s to 3 s
    MinimumThroughput = 2                                 // ↓ from 20 to 2
}
```

### ❌ "Rate limiting too aggressive"

**Fix:**
```csharp
var options = new AdaptiveTrafficControlOptions
{
    InitialMaxRequestsPerSecond = 5000,  // ↑ raise base limit
    ScoreSmoothingFactor        = 0.7    // ↑ less aggressive (0.3 = aggressive)
};
```

### ❌ "Timeout exceptions instead of BrokenCircuitException"

**Cause:** Circuit isn't opening quickly enough.

**Fix:**
- Decrease application timeout.
- Increase circuit sensitivity.
- Ensure timeout propagates correctly.

---

## Production Checklist

### ✅ Before Deploy

- [ ] **Thresholds tested** in staging with realistic load.
- [ ] **Logging configured** – `OnOpened`, `OnClosed`, `OnHalfOpened` must emit logs.
- [ ] **Metrics exported** – send health score & telemetry to observability platform.
- [ ] **Timeout configured** in the app > `BreakDuration`.
- [ ] **Fallback strategy** defined for when the circuit opens.
- [ ] **Graceful degradation** verified.

### 📊 Recommended Monitoring

```csharp
// Export telemetry each cycle
_ = Task.Run(async () =>
{
    while (!cancellationToken.IsCancellationRequested)
    {
        var telemetry = await adaptive.GetLatestTelemetryAsync();

        metrics.RecordGauge("circuit_breaker.error_rate", telemetry.ErrorRate);
        metrics.RecordGauge("circuit_breaker.latency_p99", telemetry.P99LatencyMs);
        metrics.RecordGauge("circuit_breaker.health_score", adaptive.CurrentHealthScore.Value);
        metrics.RecordGauge("circuit_breaker.state", (int)breaker.State);

        await Task.Delay(TimeSpan.FromSeconds(5));
    }
});
```

### 🚨 Recommended Alerts

| Alert               | Condition                   | Action      |
|---------------------|-----------------------------|-------------|
| Circuit **OPEN**    | State = Open > 60 s         | Page        |
| Cascading Failures  | Error rate > 80 % for 30 s  | Page        |
| Health **Critical** | Health score < 0.2          | Warning     |
| Timeout spike       | P99 latency > 10 s          | Investigate |

---

## Full Production Example

```csharp
services.AddAdaptiveCircuitBreaker(
    circuitOptions: new CircuitBreakerOptions
    {
        FailureRatio      = 0.5,
        MinimumThroughput = 10,
        SamplingDuration  = TimeSpan.FromSeconds(10),
        BreakDuration     = TimeSpan.FromSeconds(30),
        OnOpened   = duration =>
            logger.LogCritical(
                "Circuit OPEN for downstream PaymentAPI - {Duration}s recovery window",
                duration.TotalSeconds),
        OnClosed   = () =>
            logger.LogInformation("Circuit CLOSED - PaymentAPI recovered"),
        OnHalfOpened = () =>
            logger.LogWarning("Circuit HALF-OPEN - testing PaymentAPI...")
    },
    adaptiveOptions: new AdaptiveTrafficControlOptions
    {
        InitialMaxRequestsPerSecond = 2000,
        InitialMaxConcurrency       = 200,
        ControlLoopInterval         = TimeSpan.FromMilliseconds(200),
        TelemetryWindow             = TimeSpan.FromSeconds(60)
    },
    resourceName: "PaymentServiceAPI"
);

// Register health‑check endpoint
app.MapGet("/health/circuit-breaker", async (AdaptiveCircuitBreakerDecorator adaptive) =>
{
    var telemetry = await adaptive.GetLatestTelemetryAsync();
    return Results.Ok(new
    {
        state     = adaptive.State.ToString(),
        health    = adaptive.CurrentHealthScore.Value,
        telemetry = new
        {
            errorRate    = telemetry.ErrorRate,
            latencyMs    = telemetry.LatencyMs,
            p99LatencyMs = telemetry.P99LatencyMs
        }
    });
});
```

---

## Additional Resources

- **Polly Documentation**: https://github.com/App-vNext/Polly
- **Circuit Breaker Pattern**: https://martinfowler.com/bliki/CircuitBreaker.html
- **GitHub Project**: *[Your repository link]*

---

**Last updated**: June 2, 2026  
**Version**: 1.0
