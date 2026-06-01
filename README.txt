===============================================================================
CIRCUITBREAKER
===============================================================================

Biblioteca .NET 10 que encapsula o Advanced Circuit Breaker do Polly v8 em uma
API simples, thread-safe e pronta para distribuição via NuGet.

-------------------------------------------------------------------------------
VISÃO GERAL
-------------------------------------------------------------------------------

Este repositório oferece:

  * `CircuitBreaker.Core` — wrapper do Circuit Breaker do Polly com API
    simplificada e integração com Dependency Injection.
  * `CircuitBreaker.Telemetry` — provedor de métricas de janela deslizante.
  * `CircuitBreaker.Adaptive` — controle adaptativo de tráfego, concorrência e
    rate limiting.
  * `CircuitBreaker.Sample` — exemplo de uso do core.
  * `CircuitBreaker.Adaptive.Sample` — exemplo de uso adaptativo.

O padrão Circuit Breaker ajuda a evitar falhas em cascata quando um serviço
externo começa a apresentar erro. Quando a taxa de falha ultrapassa um limite
configurado, o circuito abre e bloqueia novas chamadas até que o serviço
tenha tempo para se recuperar.

Principais características:

  * Sliding Window baseada em tempo
  * Thread-safe
  * Proteção contra race conditions
  * CancellationToken nativo
  * Consulta de estado em tempo real
  * Callbacks configuráveis
  * Integração com Dependency Injection
  * API simplificada
  * Factory Pattern
  * Pronto para empacotamento NuGet

-------------------------------------------------------------------------------
COMO USAR
-------------------------------------------------------------------------------

1. Restaurar pacotes:

       dotnet restore

2. Compilar:

       dotnet build

3. Executar o sample do core:

       dotnet run --project src/CircuitBreaker.Sample

4. Executar o sample adaptativo:

       dotnet run --project src/CircuitBreaker.Adaptive.Sample

-------------------------------------------------------------------------------
ARQUITETURA
-------------------------------------------------------------------------------

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

O `CircuitBreaker` atua como um wrapper fino sobre o `ResiliencePipeline` do
Polly. A máquina de estados é delegada ao Polly:

    CLOSED -> OPEN -> HALF-OPEN -> CLOSED

-------------------------------------------------------------------------------
ESTRUTURA DO REPOSITÓRIO
-------------------------------------------------------------------------------

src/
  CircuitBreaker.Core/
  CircuitBreaker.Telemetry/
  CircuitBreaker.Adaptive/
  CircuitBreaker.Sample/
  CircuitBreaker.Adaptive.Sample/

dist/        (possível saída de build ou pacote)
README.md
README.txt

-------------------------------------------------------------------------------
PROJECTOS NA SOLUTION
-------------------------------------------------------------------------------

CircuitBreaker.slnx inclui:

  * CircuitBreaker.Core
  * CircuitBreaker.Telemetry
  * CircuitBreaker.Adaptive
  * CircuitBreaker.Sample
  * CircuitBreaker.Adaptive.Sample

-------------------------------------------------------------------------------
SLIDING WINDOW
-------------------------------------------------------------------------------

O Polly utiliza uma janela temporal para calcular a taxa de falhas.

Exemplo:

    SamplingDuration = 10 segundos

    SUCESSO
    SUCESSO
    SUCESSO
    FALHA
    FALHA
    SUCESSO
    FALHA
    FALHA
    FALHA
    SUCESSO

    Total = 10 chamadas
    Falhas = 5

    FailureRatio = 50%

O circuito abre quando:

    FailureRatio >= valor configurado
    AND
    Total >= MinimumThroughput

-------------------------------------------------------------------------------
ESTADOS
-------------------------------------------------------------------------------

CLOSED
    Operação normal.

OPEN
    Todas as chamadas são bloqueadas.

HALF-OPEN
    Uma única chamada de teste é permitida.

Fluxo:

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

-------------------------------------------------------------------------------
PROTEÇÕES DE CONCORRÊNCIA
-------------------------------------------------------------------------------

Problema:
    Duas threads entrando simultaneamente em Half-Open.

Solução:
    Polly permite apenas uma requisição de teste.

Problema:
    Race condition em contadores.

Solução:
    Estruturas lock-free internas.

Problema:
    Mudança simultânea de estado.

Solução:
    Máquina de estados atômica.

-------------------------------------------------------------------------------
COMPONENTES PRINCIPAIS
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

Propriedade:

    State

-------------------------------------------------------------------------------
DEPENDÊNCIAS PRINCIPAIS
-------------------------------------------------------------------------------

  * Polly 8.6.6
  * Microsoft.Extensions.DependencyInjection 10.0.8

-------------------------------------------------------------------------------
LICENÇA
-------------------------------------------------------------------------------

Projeto distribuído para fins educacionais e de demonstração.

-------------------------------------------------------------------------------
CONFIGURAÇÃO
-------------------------------------------------------------------------------

FailureRatio
    Taxa de falha necessária para abrir o circuito.

SamplingDuration
    Janela de observação.

MinimumThroughput
    Quantidade mínima de chamadas analisadas.

BreakDuration
    Tempo que o circuito permanece aberto.

Callbacks opcionais:

    OnOpened
    OnClosed
    OnHalfOpened

-------------------------------------------------------------------------------
USO BÁSICO
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

A biblioteca suporta propagação completa do CancellationToken.

Quando utilizado corretamente, cancelamentos realizados pelo Polly também
cancelam a operação do usuário.

-------------------------------------------------------------------------------
OBSERVABILIDADE
-------------------------------------------------------------------------------

A biblioteca não gera logs automaticamente.

O consumidor define os callbacks:

    OnOpened
    OnClosed
    OnHalfOpened

Isso evita efeitos colaterais e mantém a biblioteca silenciosa por padrão.

-------------------------------------------------------------------------------
DEPENDENCY INJECTION
-------------------------------------------------------------------------------

Recomendado registrar como Singleton.

Exemplo:

    services.AddSingleton<ICircuitBreaker>(...)

Também funciona naturalmente com Decorator Pattern.

-------------------------------------------------------------------------------
DECORATOR PATTERN
-------------------------------------------------------------------------------

Fluxo típico:

    Cliente
        |
        v

    MyServiceDecorator
        |
        v

    Circuit Breaker
        |
        v

    Serviço Real

O Decorator adiciona resiliência sem alterar o serviço original.

-------------------------------------------------------------------------------
BUILD
-------------------------------------------------------------------------------

Compilar:

    dotnet build src/CircuitBreaker.slnx

-------------------------------------------------------------------------------
EXECUTAR DEMO
-------------------------------------------------------------------------------

    dotnet run --project \
        src/CircuitBreaker.Sample/CircuitBreaker.Sample.csproj

-------------------------------------------------------------------------------
GERAR PACOTE NUGET
-------------------------------------------------------------------------------

    dotnet pack \
        src/CircuitBreaker.Core/CircuitBreaker.Core.csproj \
        -c Release \
        -o ./dist

-------------------------------------------------------------------------------
CONFIGURAÇÕES SUGERIDAS
-------------------------------------------------------------------------------

Produção Conservadora

    FailureRatio      = 0.25
    SamplingDuration  = 30s
    MinimumThroughput = 20
    BreakDuration     = 30s

Produção Agressiva

    FailureRatio      = 0.50
    SamplingDuration  = 10s
    MinimumThroughput = 8
    BreakDuration     = 5s

Serviço Crítico

    FailureRatio      = 0.10
    SamplingDuration  = 60s
    MinimumThroughput = 50
    BreakDuration     = 60s

-------------------------------------------------------------------------------
DECISÕES TÉCNICAS
-------------------------------------------------------------------------------

Por que Polly v8?

    * Implementação madura
    * Sliding Window nativa
    * Controle robusto de concorrência
    * Menor custo de manutenção
    * Comunidade ativa

Por que Wrapper?

    * API simplificada
    * Menor acoplamento
    * Facilidade para troca futura de engine
    * Configuração centralizada
    * State tracking próprio

Por que volatile int?

    * Lock-free
    * Leitura atômica
    * Thread-safe
    * Baixo overhead

-------------------------------------------------------------------------------
EVOLUÇÃO FUTURA
-------------------------------------------------------------------------------

Possível evolução para um sistema de controle adaptativo de tráfego baseado em:

    * Error Rate
    * Throughput
    * Latência
    * P95
    * P99
    * Timeouts
    * Saturação de recursos

Conceito:

    Health Score = 0.0 .. 1.0

Ações futuras possíveis:

    * Rate Limiting
    * Concurrency Control
    * Request Shedding
    * Circuit Breaker

Nesta arquitetura o Circuit Breaker torna-se a última camada de proteção.

-------------------------------------------------------------------------------
DEPENDÊNCIAS
-------------------------------------------------------------------------------

Polly
    Versão 8.6.6

Microsoft.Extensions.DependencyInjection
    Versão 10.0.8

-------------------------------------------------------------------------------
LICENÇA
-------------------------------------------------------------------------------

Projeto distribuído para fins educacionais e demonstração.

===============================================================================
EOF
===============================================================================