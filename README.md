# CircuitBreaker

> Biblioteca .NET 10 que encapsula o **Advanced Circuit Breaker** do **Polly v8** em uma API simples, consistente e pronta para distribuição via NuGet.

---

## Visão Geral

Este repositório contém uma camada de abstração sobre o Circuit Breaker do Polly, com foco em:

- API simplificada
- integração com `CancellationToken`
- métricas de telemetria
- controle adaptativo de tráfego
- compatibilidade com `Dependency Injection`

### Projetos incluídos

- `CircuitBreaker.Core` — wrapper do Polly com Circuit Breaker e factory
- `CircuitBreaker.Telemetry` — provedor de métricas e janela deslizante
- `CircuitBreaker.Adaptive` — controle adaptativo de tráfego, rate limiting e concorrência
- `CircuitBreaker.Sample` — exemplo de uso do core
- `CircuitBreaker.Adaptive.Sample` — exemplo de uso adaptativo

---

## Como usar

1. Restaurar pacotes:

```bash
dotnet restore
```

2. Compilar todos os projetos:

```bash
dotnet build
```

3. Executar o sample do core:

```bash
dotnet run --project src/CircuitBreaker.Sample
```

4. Executar o sample adaptativo:

```bash
dotnet run --project src/CircuitBreaker.Adaptive.Sample
```

---

## Arquitetura

```text
Sua Aplicação
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

O `CircuitBreaker` atua como um wrapper fino sobre o `ResiliencePipeline` do Polly.

---

## Máquina de Estados

```text
CLOSED
   |
   | taxa de falha excedida
   v
OPEN
   |
   | BreakDuration expirado
   v
HALF-OPEN
   |
   +-- sucesso --> CLOSED
   |
   +-- falha ----> OPEN
```

### Estados

- `CLOSED` — operação normal
- `OPEN` — chamadas bloqueadas
- `HALF-OPEN` — chamada de teste permitida

---

## Sliding Window

O Polly utiliza uma janela temporal para calcular a taxa de falhas.

```text
Sampling Duration = 10 segundos

SUCESSO SUCESSO SUCESSO FALHA FALHA SUCESSO FALHA FALHA FALHA SUCESSO

Total:   10 chamadas
Falhas:   5
FailureRatio = 50%
```

O circuito abre quando:

```text
FailureRatio >= configurado
AND
Throughput >= MinimumThroughput
```

---

## Adaptive Traffic Control

A camada adaptativa (`CircuitBreaker.Adaptive`) atua antes do Circuit Breaker,
oferecendo controle de tráfego e proteção adicional.

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

### Conceito

Em vez de depender apenas dos estados discretos:

```text
Closed → Open → Half-Open
```

pode-se calcular continuamente um indicador de saúde:

```text
Health Score = 0.0 .. 1.0
```

| Score | Estado |
|------:|--------|
| 1.0   | Saudável |
| 0.8   | Leve degradação |
| 0.5   | Degradação moderada |
| 0.2   | Estado crítico |
| 0.0   | Falha severa |

---

## Estrutura do repositório

- `src/CircuitBreaker.Core`
- `src/CircuitBreaker.Telemetry`
- `src/CircuitBreaker.Adaptive`
- `src/CircuitBreaker.Sample`
- `src/CircuitBreaker.Adaptive.Sample`
- `dist/` (possível saída de build ou pacote)

---

## Dependências principais

- `Polly` 8.6.6
- `Microsoft.Extensions.DependencyInjection` 10.0.8

---

## Licença

Projeto distribuído para fins educacionais e de demonstração.

---

## Referência

Veja também `README.txt` para uma visão complementar e exemplos adicionais.
