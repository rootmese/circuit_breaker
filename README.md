# CircuitBreaker.Core

> Biblioteca .NET 10 que encapsula o **Advanced Circuit Breaker** do [Polly v8](https://github.com/App-vNext/Polly) em uma API simplificada, pronta para distribuição via NuGet.

---

## Índice

- [Visão Geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Como Funciona — Sliding Window](#como-funciona--sliding-window)
- [Componentes da Biblioteca](#componentes-da-biblioteca)
- [Uso Básico](#uso-básico)
- [CancellationToken](#cancellationtoken)
- [Observabilidade (Callbacks)](#observabilidade-callbacks)
- [Consulta de Estado](#consulta-de-estado)
- [Integração com Dependency Injection](#integração-com-dependency-injection)
- [Padrão Decorator](#padrão-decorator)
- [Aplicação de Exemplo](#aplicação-de-exemplo)
- [Build, Run e Packaging](#build-run-e-packaging)
- [Referência de Configuração](#referência-de-configuração)
- [Decisões Técnicas](#decisões-técnicas)
- [Licença](#licença)

---

## Visão Geral

O **Circuit Breaker** é um padrão de resiliência que previne falhas em cascata ao monitorar chamadas a serviços externos. Quando a taxa de erros ultrapassa um limite configurado, o circuito "abre" e bloqueia novas chamadas por um período, dando tempo ao serviço para se recuperar.

Esta biblioteca oferece:

- ✅ **Sliding Window (Janela Deslizante)** — decisões baseadas em taxa de falha estatística, não em contagem absoluta
- ✅ **Thread-safe** — toda a concorrência é gerenciada internamente pelo Polly
- ✅ **Zero race conditions** — eliminação do bug clássico de múltiplas threads entrando em Half-Open simultaneamente
- ✅ **CancellationToken nativo** — propagação completa do token de cancelamento até a ação do usuário
- ✅ **Consulta de estado** — propriedade `State` para health checks e dashboards
- ✅ **Callbacks configuráveis** — observabilidade sem `Console.WriteLine` hardcoded na lib
- ✅ **API simplificada** — `ExecuteAsync<T>()` e `ExecuteAsync()` com overloads
- ✅ **Factory pattern** — criação configurável via `CircuitBreakerFactory`
- ✅ **Pronta para NuGet** — empacotável com `dotnet pack`

---

## Arquitetura

```
┌─────────────────────────────────────────────────────────┐
│                    Sua Aplicação                        │
│                                                         │
│  IMyService ──► MyServiceDecorator ──► ICircuitBreaker  │
│                                            │            │
│                                    ┌───────┴───────┐    │
│                                    │ CircuitBreaker │    │
│                                    │   (wrapper)    │    │
│                                    └───────┬───────┘    │
│                                            │            │
│                                  ┌─────────┴─────────┐  │
│                                  │ ResiliencePipeline │  │
│                                  │   (Polly v8)      │  │
│                                  └───────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

A classe `CircuitBreaker` é um **thin wrapper** sobre o `ResiliencePipeline` do Polly. Toda a lógica de estado (Closed → Open → Half-Open → Closed) é delegada ao engine do Polly, que resolve nativamente problemas de concorrência e race conditions.

---

## Estrutura do Projeto

```
circuit_breaker/
├── src/
│   ├── CircuitBreaker.slnx              # Solution (.NET 10 slnx format)
│   ├── CircuitBreaker.Core/             # 📦 Biblioteca (NuGet package)
│   │   ├── CircuitBreaker.Core.csproj
│   │   ├── ICircuitBreaker.cs           # Interface pública (com overloads de CancellationToken)
│   │   ├── CircuitBreaker.cs            # Wrapper sobre ResiliencePipeline
│   │   ├── CircuitBreakerOptions.cs     # Configuração da sliding window + callbacks
│   │   ├── CircuitBreakerFactory.cs     # Factory com state tracking e callbacks
│   │   └── CircuitState.cs              # Enum: Closed, Open, HalfOpen
│   └── CircuitBreaker.Sample/           # 🎮 App console de demonstração
│       ├── CircuitBreaker.Sample.csproj
│       ├── Program.cs                   # Ponto de entrada com DI e state query
│       ├── IMyService.cs                # Interface do serviço
│       ├── RealService.cs               # Serviço que simula falhas
│       ├── FallbackService.cs           # Serviço de fallback
│       └── MyServiceDecorator.cs        # Decorator com circuit breaker
├── dist/                                # Pacotes NuGet gerados
├── .gitignore
└── README.md
```

---

## Como Funciona — Sliding Window

Diferente de implementações simples que contam falhas absolutas (ex: "2 falhas = abre"), o **Advanced Circuit Breaker** do Polly usa uma **janela deslizante temporal**:

```
  Sampling Duration (10s)
  ◄──────────────────────►

  ✅ ✅ ✅ ❌ ❌ ✅ ❌ ❌ ❌ ✅
  │                         │
  └── Taxa de falha = 50% ──┘
       (5 falhas / 10 total)

  Se FailureRatio ≥ 0.5 E total ≥ MinimumThroughput → ABRE
```

### Transições de Estado

```
   ┌──────────┐  taxa de falha   ┌──────────┐  BreakDuration   ┌──────────────┐
   │  CLOSED  │ ≥ FailureRatio   │   OPEN   │   expira         │  HALF-OPEN   │
   │          ├─────────────────►│          ├──────────────────►│              │
   │ (normal) │  AND throughput  │ (bloqueia│                   │ (testa 1 req)│
   └──────────┘  ≥ minimum      │  tudo)   │                   └──────┬───────┘
        ▲                        └──────────┘                          │
        │                             ▲                                │
        │      requisição ok          │        requisição falha        │
        └─────────────────────────────┼────────────────────────────────┘
                                      └────────────────────────────────
```

### Proteções de Concorrência

| Cenário                                    | Solução do Polly                              |
|--------------------------------------------|-----------------------------------------------|
| Duas threads entrando em Half-Open         | Polly permite apenas **uma** requisição teste |
| Contador de falhas com race condition      | Internamente usa estruturas lock-free          |
| Transição de estado simultânea             | Máquina de estados atômica                    |

---

## Componentes da Biblioteca

### `CircuitState` (Enum)

```csharp
public enum CircuitState
{
    Closed,    // Requisições fluem normalmente
    Open,      // Todas as requisições são bloqueadas
    HalfOpen   // Uma requisição de teste é permitida
}
```

### `ICircuitBreaker`

Interface pública com 4 métodos e consulta de estado:

```csharp
public interface ICircuitBreaker
{
    // Consulta de estado para health checks e dashboards
    CircuitState State { get; }

    // Overloads simples (sem CancellationToken)
    Task<T> ExecuteAsync<T>(Func<Task<T>> action);
    Task ExecuteAsync(Func<Task> action);

    // Overloads com CancellationToken propagado pelo pipeline
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
```

Quando o circuito está aberto, todos os métodos lançam `Polly.CircuitBreaker.BrokenCircuitException`.

### `CircuitBreakerOptions`

Configuração da sliding window e callbacks de observabilidade:

```csharp
public class CircuitBreakerOptions
{
    // Configuração da Sliding Window
    public double   FailureRatio     { get; set; } = 0.5;              // 50%
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(10);
    public int      MinimumThroughput{ get; set; } = 8;
    public TimeSpan BreakDuration    { get; set; } = TimeSpan.FromSeconds(5);

    // Callbacks de observabilidade (opcionais)
    public Action<TimeSpan>? OnOpened     { get; set; }   // recebe o BreakDuration
    public Action?           OnClosed     { get; set; }
    public Action?           OnHalfOpened { get; set; }
}
```

| Propriedade         | Descrição                                                                 | Default  |
|---------------------|---------------------------------------------------------------------------|----------|
| `FailureRatio`      | Taxa de falha (0.0 a 1.0) necessária para abrir o circuito               | `0.5`    |
| `SamplingDuration`  | Duração da janela deslizante de amostragem                                | `10s`    |
| `MinimumThroughput` | Mínimo de chamadas na janela antes que o circuito possa ser ativado       | `8`      |
| `BreakDuration`     | Tempo que o circuito fica aberto antes de testar novamente (Half-Open)    | `5s`     |
| `OnOpened`          | Callback quando o circuito abre (recebe o `BreakDuration`)               | `null`   |
| `OnClosed`          | Callback quando o circuito fecha (sistema recuperado)                     | `null`   |
| `OnHalfOpened`      | Callback quando o circuito entra em Half-Open (testando)                  | `null`   |

### `CircuitBreakerFactory`

Factory estática que monta o `ResiliencePipeline`, conecta o state tracking interno e os callbacks do consumidor:

```csharp
var breaker = CircuitBreakerFactory.Create(
    new CircuitBreakerOptions
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(10),
        MinimumThroughput = 8,
        BreakDuration = TimeSpan.FromSeconds(5),

        // Você define o que acontece em cada transição
        OnOpened = breakDuration =>
            logger.LogWarning("Circuit OPENED for {Duration}s", breakDuration.TotalSeconds),
        OnClosed = () =>
            logger.LogInformation("Circuit CLOSED. Healthy."),
        OnHalfOpened = () =>
            logger.LogInformation("Circuit HALF-OPEN. Testing...")
    },
    resourceName: "PaymentAPI"
);
```

### `CircuitBreaker`

Wrapper fino sobre `ResiliencePipeline` com state tracking via `volatile int`:

```csharp
public class CircuitBreaker : ICircuitBreaker
{
    private readonly ResiliencePipeline _pipeline;
    private volatile int _state; // thread-safe state tracking

    public CircuitState State => (CircuitState)_state;

    // State é atualizado automaticamente pela Factory via callbacks do Polly
    internal void UpdateState(CircuitState state) => _state = (int)state;
}
```

---

## Uso Básico

### Execução Simples

```csharp
using CircuitBreaker.Core;

var breaker = CircuitBreakerFactory.Create(
    new CircuitBreakerOptions { FailureRatio = 0.5, MinimumThroughput = 4 },
    resourceName: "PaymentAPI"
);

try
{
    string result = await breaker.ExecuteAsync(async () =>
        await httpClient.GetStringAsync("https://api.exemplo.com/pagamento")
    );
    Console.WriteLine($"Resultado: {result}");
}
catch (Polly.CircuitBreaker.BrokenCircuitException)
{
    Console.WriteLine("Circuito aberto! Usando fallback...");
}
```

---

## CancellationToken

Os overloads com `CancellationToken` propagam o token do pipeline do Polly até a sua ação, garantindo cancelamento cooperativo:

```csharp
// O token é propagado pelo Polly e repassado à sua action
var result = await breaker.ExecuteAsync(async (CancellationToken ct) =>
{
    return await httpClient.GetStringAsync("https://api.exemplo.com/dados", ct);
}, cancellationToken: cts.Token);
```

**Sem o CancellationToken** (overload simples), se o Polly cancelar a operação internamente, a sua action continua rodando em background. Com o token propagado, a action é cancelada junto.

---

## Observabilidade (Callbacks)

A biblioteca **não faz log por conta própria**. Você define o que acontece em cada transição de estado via callbacks no `CircuitBreakerOptions`:

```csharp
var options = new CircuitBreakerOptions
{
    OnOpened = breakDuration =>
    {
        logger.LogWarning("⚡ Circuit abriu por {Sec}s", breakDuration.TotalSeconds);
        metrics.IncrementCounter("circuit_opened");
    },
    OnClosed = () =>
    {
        logger.LogInformation("✅ Circuit fechou");
        metrics.IncrementCounter("circuit_closed");
    },
    OnHalfOpened = () =>
    {
        logger.LogInformation("🔍 Circuit em teste (Half-Open)");
    }
};
```

Se você não definir callbacks, nada acontece (sem side effects).

---

## Consulta de Estado

A propriedade `State` permite consultar o estado atual para health checks, dashboards ou lógica condicional:

```csharp
var breaker = serviceProvider.GetRequiredService<ICircuitBreaker>();

// Health check endpoint
app.MapGet("/health", () =>
{
    var state = breaker.State;
    return state == CircuitState.Closed
        ? Results.Ok(new { status = "healthy", circuit = state.ToString() })
        : Results.StatusCode(503, new { status = "degraded", circuit = state.ToString() });
});

// Lógica condicional
if (breaker.State == CircuitState.Open)
{
    return await fallbackService.GetDataAsync();
}
```

---

## Integração com Dependency Injection

```csharp
using CircuitBreaker.Core;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Registrar o circuit breaker como singleton
services.AddSingleton<ICircuitBreaker>(provider =>
    CircuitBreakerFactory.Create(
        new CircuitBreakerOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            MinimumThroughput = 8,
            BreakDuration = TimeSpan.FromSeconds(5),
            OnOpened = d => Console.WriteLine($"⚡ Opened for {d.TotalSeconds}s"),
            OnClosed = () => Console.WriteLine("✅ Closed"),
            OnHalfOpened = () => Console.WriteLine("🔍 Half-Open")
        },
        resourceName: "MyService"
    )
);

// Registrar o serviço com decorator
services.AddTransient<RealService>();
services.AddTransient<FallbackService>();
services.AddTransient<IMyService>(provider =>
{
    var breaker = provider.GetRequiredService<ICircuitBreaker>();
    var real = provider.GetRequiredService<RealService>();
    return new MyServiceDecorator(real, breaker);
});
```

---

## Padrão Decorator

O `MyServiceDecorator` envolve qualquer `IMyService` com proteção do circuit breaker:

```csharp
public class MyServiceDecorator : IMyService
{
    private readonly IMyService _realService;
    private readonly ICircuitBreaker _breaker;

    public MyServiceDecorator(IMyService realService, ICircuitBreaker breaker)
    {
        _realService = realService;
        _breaker = breaker;
    }

    public async Task<string> GetDataAsync()
    {
        try
        {
            return await _breaker.ExecuteAsync(() => _realService.GetDataAsync());
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            Console.WriteLine($"Bloqueado pelo circuit breaker: {ex.Message}");
            throw;
        }
    }
}
```

---

## Aplicação de Exemplo

O projeto `CircuitBreaker.Sample` demonstra o fluxo completo com state tracking visível:

1. **`RealService`** — simula um serviço instável que falha nas 2 primeiras chamadas
2. **`FallbackService`** — retorna dados em cache como modo de degradação
3. **`MyServiceDecorator`** — aplica o circuit breaker sobre o serviço real

O `Program.cs` executa 8 chamadas sequenciais exibindo o estado do circuito em cada passo:

```
Call #1  │ State: Closed   → Falha (attempt 1)        → Fallback
Call #2  │ State: Closed   → Falha (attempt 2)        → ⚡ Circuit OPENS → Fallback
Call #3  │ State: Open     → BrokenCircuitException   → Fallback
Call #4  │ State: Open     → BrokenCircuitException   → Fallback
Call #5  │ State: Open     → BrokenCircuitException   → Fallback
Call #6  │ State: Open     → BrokenCircuitException   → Fallback
         │ ⏳ Aguarda 6s (BreakDuration = 5s)
Call #7  │ State: Open     → 🔍 Half-Open → attempt 3 → ✅ Sucesso → Circuit CLOSES
Call #8  │ State: Closed   → attempt 4 → Sucesso normal
Final    │ State: Closed ✅
```

---

## Build, Run e Packaging

### Pré-requisitos

- [.NET 10.0 SDK](https://dotnet.microsoft.com/) ou superior

### Build

```bash
dotnet build src/CircuitBreaker.slnx
```

### Executar Demo

```bash
dotnet run --project src/CircuitBreaker.Sample/CircuitBreaker.Sample.csproj
```

### Gerar Pacote NuGet

```bash
dotnet pack src/CircuitBreaker.Core/CircuitBreaker.Core.csproj -c Release -o ./dist
```

O pacote `.nupkg` será gerado na pasta `dist/`.

---

## Referência de Configuração

### Cenários Comuns

| Cenário                     | FailureRatio | SamplingDuration | MinimumThroughput | BreakDuration |
|-----------------------------|:------------:|:----------------:|:-----------------:|:-------------:|
| **Produção (conservador)**  | `0.25`       | `30s`            | `20`              | `30s`         |
| **Produção (agressivo)**    | `0.5`        | `10s`            | `8`               | `5s`          |
| **Testes / Demo**           | `0.5`        | `10s`            | `2`               | `5s`          |
| **Serviço crítico**         | `0.1`        | `60s`            | `50`              | `60s`         |

### Dicas

- **`MinimumThroughput` baixo** = reage mais rápido, mas pode ter falsos positivos
- **`SamplingDuration` curto** = mais sensível a picos momentâneos
- **`BreakDuration` longo** = mais tempo para o serviço se recuperar, mas maior latência de retorno
- **Callbacks `null`** = nenhum side effect — a lib é silenciosa por padrão

---

## Decisões Técnicas

### Por que Polly v8 e não implementação custom?

| Aspecto                   | Custom                                | Polly v8                                |
|---------------------------|---------------------------------------|-----------------------------------------|
| Race conditions           | Requer `lock` / `Interlocked`         | Resolvido internamente                  |
| Half-Open com 2+ threads  | Bug clássico e difícil de reproduzir  | Apenas 1 requisição teste permitida     |
| Sliding window            | Implementação complexa                | Nativo (`AdvancedCircuitBreaker`)        |
| Testabilidade             | Mock manual                           | `ResiliencePipeline` injetável          |
| Manutenção                | Código próprio                        | Mantido pela comunidade OSS             |

### Por que um wrapper e não usar Polly direto?

- **Abstração** — consumidores da biblioteca não precisam conhecer Polly
- **Configuração centralizada** — `CircuitBreakerOptions` simplifica o setup com callbacks integrados
- **Observabilidade opt-in** — a lib não faz log sozinha; o consumidor define o que acontece
- **State tracking** — propriedade `State` para health checks sem expor internals do Polly
- **CancellationToken** — overloads que propagam corretamente o token até a ação final
- **Substituibilidade** — a interface `ICircuitBreaker` permite trocar a implementação sem afetar consumidores

### Por que `volatile int` para o estado?

O estado é rastreado via `volatile int` (castado para `CircuitState` enum) por ser:
- **Lock-free** — leituras atômicas sem custo de sincronização
- **Thread-safe** — `volatile` garante visibilidade entre threads
- **Consistente** — atualizado pelos callbacks do Polly que são invocados de forma serializada

---

## Dependências

| Pacote                                      | Versão   | Projeto         |
|---------------------------------------------|----------|-----------------|
| `Polly`                                     | `8.6.6`  | Core            |
| `Microsoft.Extensions.DependencyInjection`  | `10.0.8` | Sample          |

> **Nota:** O Sample recebe `Polly` como dependência transitiva do Core — não é necessário referenciá-lo diretamente.

---

## Licença

Este projeto é distribuído para fins educacionais e de demonstração.
