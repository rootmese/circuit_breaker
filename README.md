# CircuitBreaker.Core

> Biblioteca .NET 10 que encapsula o **Advanced Circuit Breaker** do **Polly v8** em uma API simples, consistente e pronta para distribuição via NuGet.

---

## Visão Geral

O objetivo desta biblioteca é fornecer uma camada de abstração sobre o Circuit Breaker do Polly, reduzindo a complexidade de configuração e oferecendo uma interface estável para aplicações corporativas.

### Principais Recursos

- Sliding Window baseada em taxa de falha
- Thread-safe
- Proteção contra race conditions
- Integração nativa com `CancellationToken`
- Callbacks de observabilidade
- Consulta de estado (`Closed`, `Open`, `HalfOpen`)
- Factory Pattern
- Compatível com Dependency Injection
- Pronta para empacotamento NuGet

---

## Arquitetura

```text
┌─────────────────────────────────────────────────────────┐
│                    Sua Aplicação                        │
│                                                         │
│  IMyService ──► MyServiceDecorator ──► ICircuitBreaker  │
│                                            │            │
│                                    ┌───────┴───────┐    │
│                                    │ CircuitBreaker│    │
│                                    │   (wrapper)   │    │
│                                    └───────┬───────┘    │
│                                            │            │
│                                  ┌─────────┴─────────┐  │
│                                  │ ResiliencePipeline│  │
│                                  │    Polly v8       │  │
│                                  └───────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

---

## Máquina de Estados

```text
                 Failure Ratio Atingido
        ┌────────────────────────────────────┐
        │                                    ▼

┌────────────┐                      ┌────────────┐
│   CLOSED   │                      │    OPEN    │
│ Operação   │                      │ Bloqueado  │
│  normal    │                      │            │
└─────┬──────┘                      └─────┬──────┘
      ▲                                   │
      │                                   │ BreakDuration
      │                                   │ expirou
      │                                   ▼
      │                            ┌────────────┐
      │                            │ HALF-OPEN  │
      │                            │ Teste de   │
      │                            │ recuperação│
      │                            └─────┬──────┘
      │                                  │
      │ Sucesso                          │ Falha
      └──────────────────────────────────┘
```

---

## Sliding Window

O Polly utiliza uma janela temporal para calcular a taxa de falhas.

```text
Sampling Duration (10s)

✅ ✅ ✅ ❌ ❌ ✅ ❌ ❌ ❌ ✅

Total:   10 chamadas
Falhas:   5 chamadas

Failure Ratio = 50%
```

O circuito abre quando:

```text
FailureRatio >= configurado
AND
Throughput >= MinimumThroughput
```

---

## Projetos da solution

| Projeto | Descrição |
|---------|-----------|
| `CircuitBreaker.Core` | Wrapper Polly v8 + factory |
| `CircuitBreaker.Telemetry` | Janela deslizante de métricas |
| `CircuitBreaker.Adaptive` | Rate limit, concorrência, health score, decorator |
| `CircuitBreaker.Sample` | Demo do core |
| `CircuitBreaker.Adaptive.Sample` | Demo adaptativo |

```bash
dotnet run --project src/CircuitBreaker.Sample
dotnet run --project src/CircuitBreaker.Adaptive.Sample
```

## Adaptive Traffic Control

Camada opcional (`CircuitBreaker.Adaptive`) que atua **antes** do circuit breaker (última linha de defesa).

```text
Telemetry
    │
    ▼
Health Score Calculator
    │
    ▼
Adaptive Traffic Controller
    │
    ├── Rate Limiting
    ├── Concurrency Control
    ├── Request Shedding
    │
    ▼
Circuit Breaker
(Ultimate Protection Layer)
```

### Conceito

Em vez de trabalhar apenas com estados discretos:

```text
Closed → Open → Half-Open
```

o sistema poderia calcular continuamente um indicador de saúde:

```text
Health Score = 0.0 .. 1.0
```

| Score | Estado |
|--------|---------|
| 1.0 | Saudável |
| 0.8 | Leve degradação |
| 0.5 | Degradação moderada |
| 0.2 | Estado crítico |
| 0.0 | Falha severa |

### Possíveis Ações

| Score | Ação |
|---------|---------|
| 0.8 | Redução leve de throughput |
| 0.5 | Limitação de concorrência |
| 0.2 | Rejeição seletiva |
| 0.0 | Abertura do Circuit Breaker |

---

## Dependências

| Pacote | Versão |
|---------|---------|
| Polly | 8.6.6 |
| Microsoft.Extensions.DependencyInjection | 10.0.8 |

---

## Licença

Projeto distribuído para fins educacionais e de demonstração.

---

## Detalhamento técnico

Ver arquivo README.txt
