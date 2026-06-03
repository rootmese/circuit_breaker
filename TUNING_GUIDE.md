# Circuit Breaker - Guia de Tuning e Boas Práticas

## 📋 Índice
1. [Conceitos Fundamentais](#conceitos-fundamentais)
2. [Configuração do Circuit Breaker](#configuração-do-circuit-breaker)
3. [Tuning Adaptativo](#tuning-adaptativo)
4. [Health Score](#health-score)
5. [Resolução de Problemas](#resolução-de-problemas)
6. [Checklist de Produção](#checklist-de-produção)

---

## Conceitos Fundamentais

### Estados do Circuit Breaker

O Circuit Breaker possui 3 estados:

| Estado | Comportamento | Transição |
|--------|--------------|-----------|
| **CLOSED** | Requisições fluem normalmente | → OPEN quando failure ratio excede threshold |
| **OPEN** | Todas as requisições são bloqueadas | → HALF-OPEN após BreakDuration |
| **HALF-OPEN** | Uma requisição teste é permitida | → CLOSED se suceder, OPEN se falhar |

### Parâmetros Essenciais

- **FailureRatio**: Taxa de falha que dispara abertura (0.5 = 50%)
- **MinimumThroughput**: Número mínimo de chamadas para avaliar ratio
- **SamplingDuration**: Janela de tempo para monitoramento
- **BreakDuration**: Tempo que circuit permanece aberto

---

## Configuração do Circuit Breaker

### Configuração Padrão (Recomendado para APIs)

```csharp
var breaker = CircuitBreakerFactory.Create(
    new CircuitBreakerOptions
    {
        FailureRatio = 0.5,                      // 50% de falhas
        MinimumThroughput = 8,                   // Mínimo 8 calls
        SamplingDuration = TimeSpan.FromSeconds(10),
        BreakDuration = TimeSpan.FromSeconds(5),
        
        OnOpened = duration => 
            logger.LogWarning($"Circuit opened for {duration.TotalSeconds}s"),
        OnClosed = () => 
            logger.LogInformation("Circuit closed - service recovered"),
        OnHalfOpened = () => 
            logger.LogInformation("Circuit half-open - testing service")
    },
    resourceName: "PaymentAPI"
);
```

### Profiles Recomendados

#### 🟢 **Resiliente (Default)**
Para APIs externas confiáveis, backgrounds jobs:
```
FailureRatio: 0.5 (50%)
MinimumThroughput: 8
SamplingDuration: 10s
BreakDuration: 5s
```

#### 🟡 **Moderado (Serviços Internos)**
Para microserviços instáveis ou legacy:
```
FailureRatio: 0.3 (30%)
MinimumThroughput: 5
SamplingDuration: 5s
BreakDuration: 10s
```

#### 🔴 **Agressivo (Crítico)**
Para APIs de missão crítica:
```
FailureRatio: 0.1 (10%)
MinimumThroughput: 2
SamplingDuration: 3s
BreakDuration: 30s
```

### Cálculo de Throughput Mínimo

**Fórmula**: `MinimumThroughput = (ExpectedRPS * SamplingDuration.TotalSeconds) / 4`

**Exemplo**: Para 100 RPS esperadas
- SamplingDuration = 10s
- MinimumThroughput = (100 * 10) / 4 = **250 calls**

---

## Tuning Adaptativo

### Uso Básico

```csharp
services.AddAdaptiveCircuitBreaker(
    circuitOptions: new CircuitBreakerOptions { /* ... */ },
    adaptiveOptions: new AdaptiveTrafficControlOptions
    {
        InitialMaxRequestsPerSecond = 1000,
        InitialMaxConcurrency = 100,
        ControlLoopInterval = TimeSpan.FromMilliseconds(100),
        TelemetryWindow = TimeSpan.FromSeconds(30)
    }
);
```

### Mapas de Controle Adaptativo

O sistema mapeia health score para limites de traffic:

**Health Score → Rate Limit**
- `1.0` (Saudável) → 100% dos permits
- `0.8` → 90%
- `0.5` (Degraded) → 60%
- `0.2` (Crítico) → 10%
- `< 0.1` → Bloqueado

**Health Score → Concurrency**
- `1.0` → 100% do máximo
- `0.5` → 50%
- `< 0.1` → Permitir apenas 1

### Tuning Fino dos Limites Iniciais

```csharp
// Para high-throughput systems
var options = new AdaptiveTrafficControlOptions
{
    InitialMaxRequestsPerSecond = 5000,  // Ajustar conforme RPS esperado
    InitialMaxConcurrency = 500,         // Aumentar se conexões forem caras
    ControlLoopInterval = TimeSpan.FromMilliseconds(50),  // Mais sensível
    ScoreSmoothingFactor = 0.3 // 0.0-1.0: quão rápido responde
};
```

---

## Health Score

### Componentes da Saúde

O health score é uma média ponderada de 6 métricas:

| Métrica | Peso | Threshold Saudável | Warning | Crítico |
|---------|------|-------------------|---------|---------|
| ErrorRate | 35% | < 5% | < 10% | < 25% |
| Latency | 20% | < 100ms | < 200ms | < 500ms |
| P99Latency | 15% | < 200ms | < 400ms | < 800ms |
| TimeoutRate | 10% | < 2% | < 5% | < 10% |
| ResourceSaturation | 5% | < 30% | < 60% | < 80% |
| Throughput | 15% | > 1000 req/s | > 500 | > 100 |

### Personalizar Thresholds

```csharp
var calculator = new HealthScoreCalculator();

// Mudar threshold de error rate para serviço lenient
calculator.ConfigureThreshold(
    metric: "ErrorRate",
    healthy: 0.1,      // 10% é saudável para este serviço
    warning: 0.2,
    critical: 0.5
);

// Aumentar peso de latência se for crítico
// (requer recompilação - alternativa: usar configuração externa)
```

### Diagnóstico do Health Score

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

## Resolução de Problemas

### ❌ "Circuit abre muito frequentemente"

**Causas possíveis:**
- FailureRatio muito baixo (0.1 = 10%)
- MinimumThroughput muito baixo
- Limites de timeout muito curtos

**Solução:**
```csharp
// Aumentar tolerance
new CircuitBreakerOptions
{
    FailureRatio = 0.5,          // ↑ De 0.1 para 0.5
    MinimumThroughput = 10,      // ↑ De 2 para 10
    BreakDuration = TimeSpan.FromSeconds(10)  // ↑ Mais tempo para recover
}
```

### ❌ "Circuit nunca abre (cascading failures)"

**Causas possíveis:**
- FailureRatio muito alto (0.9 = 90%)
- SamplingDuration muito longa
- Erro não está sendo registrado

**Solução:**
```csharp
// Aumentar sensibilidade
new CircuitBreakerOptions
{
    FailureRatio = 0.3,                    // ↓ De 0.9 para 0.3
    SamplingDuration = TimeSpan.FromSeconds(3),  // ↓ De 30s para 3s
    MinimumThroughput = 2                  // ↓ De 20 para 2
}
```

### ❌ "Rate limiting muito agressivo"

**Solução:**
```csharp
var options = new AdaptiveTrafficControlOptions
{
    InitialMaxRequestsPerSecond = 5000,  // ↑ Aumentar limite base
    ScoreSmoothingFactor = 0.7            // ↑ Menos agressivo (0.3 = agressivo)
};
```

### ❌ "Timeout exceptions ao invés de BrokenCircuitException"

**Causa:** Circuit não está abrindo a tempo.

**Solução:**
- Reduzir timeout da aplicação
- Aumentar sensibilidade do circuit breaker
- Verificar se timeout está sendo propagado corretamente

---

## Checklist de Produção

### ✅ Antes de Deploy

- [ ] **Thresholds testados** em staging com carga realista
- [ ] **Logging configurado** - OnOpened, OnClosed, OnHalfOpened devem registrar
- [ ] **Métricas exportadas** - Enviar health score e telemetry para observability
- [ ] **Timeout configurado** na aplicação > tempo de BreakDuration
- [ ] **Fallback strategy** definida para quando circuit abre
- [ ] **Graceful degradation** testada

### 📊 Monitoramento Recomendado

```csharp
// Exportar telemetria a cada ciclo
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

### 🚨 Alertas Recomendados

| Alerta | Condição | Action |
|--------|----------|--------|
| Circuit OPEN | State = Open por > 60s | Página |
| Cascading Failures | Error rate > 80% por 30s | Página |
| Health Critical | Health score < 0.2 | Warning |
| Timeout spike | P99 > 10s | Investigate |

---

## Exemplo Completo de Produção

```csharp
services.AddAdaptiveCircuitBreaker(
    circuitOptions: new CircuitBreakerOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(10),
        BreakDuration = TimeSpan.FromSeconds(30),
        OnOpened = duration =>
            logger.LogCritical(
                "Circuit OPEN for downstream PaymentAPI - {Duration}s recovery window",
                duration.TotalSeconds),
        OnClosed = () =>
            logger.LogInformation("Circuit CLOSED - PaymentAPI recovered"),
        OnHalfOpened = () =>
            logger.LogWarning("Circuit HALF-OPEN - testing PaymentAPI...")
    },
    adaptiveOptions: new AdaptiveTrafficControlOptions
    {
        InitialMaxRequestsPerSecond = 2000,
        InitialMaxConcurrency = 200,
        ControlLoopInterval = TimeSpan.FromMilliseconds(200),
        TelemetryWindow = TimeSpan.FromSeconds(60)
    },
    resourceName: "PaymentServiceAPI"
);

// Registrar middleware de health check
app.MapGet("/health/circuit-breaker", async (AdaptiveCircuitBreakerDecorator adaptive) =>
{
    var telemetry = await adaptive.GetLatestTelemetryAsync();
    return Results.Ok(new
    {
        state = adaptive.State.ToString(),
        health = adaptive.CurrentHealthScore.Value,
        telemetry = new
        {
            errorRate = telemetry.ErrorRate,
            latencyMs = telemetry.LatencyMs,
            p99LatencyMs = telemetry.P99LatencyMs
        }
    });
});
```

---

## Recursos Adicionais

- **Polly Documentation**: https://github.com/App-vNext/Polly
- **Circuit Breaker Pattern**: https://martinfowler.com/bliki/CircuitBreaker.html
- **Projeto GitHub**: [Seu repositório aqui]

---

**Última atualização**: Junho 2, 2026  
**Versão**: 1.0
