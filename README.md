# Circuit Breaker Core for .NET

This repository contains a lightweight, thread-safe, and generic implementation of the **Circuit Breaker** resilience pattern in C# (.NET 10). It is structured to be easily packaged and distributed as a **NuGet package**, allowing applications to prevent cascading failures when communicating with unstable external services.

---

## Project Structure

The project is organized into a .NET solution containing two projects:

1. **`CircuitBreaker.Core` (Class Library)**
   - The core library designed to be distributed as a NuGet package. It has no external dependencies.
   - **[ICircuitBreaker.cs](file:///c:/Users/User/Downloads/circuit_breaker/src/CircuitBreaker.Core/ICircuitBreaker.cs)**: Generic interface supporting action execution via delegates (`Func<Task>` and `Func<Task<T>>`).
   - **[CircuitBreaker.cs](file:///c:/Users/User/Downloads/circuit_breaker/src/CircuitBreaker.Core/CircuitBreaker.cs)**: Core execution logic implementing the pattern and catching errors.
   - **[CircuitBreakerState.cs](file:///c:/Users/User/Downloads/circuit_breaker/src/CircuitBreaker.Core/CircuitBreakerState.cs)**: Thread-safe state machine managing the transitions between `Closed`, `Open`, and `HalfOpen` states.
   - **[CircuitBrokenException.cs](file:///c:/Users/User/Downloads/circuit_breaker/src/CircuitBreaker.Core/CircuitBrokenException.cs)**: Exception thrown when execution is blocked due to an open circuit.
   - **[CircuitBreakerDelegatingHandler.cs](file:///c:/Users/User/Downloads/circuit_breaker/src/CircuitBreaker.Core/CircuitBreakerDelegatingHandler.cs)**: A custom HTTP message handler (`DelegatingHandler`) for HttpClient pipelines.

2. **`CircuitBreaker.Sample` (Console Application)**
   - A demonstration project showing how to integrate `CircuitBreaker.Core` with Dependency Injection and implement fallback flows.
   - Includes simulations for service failures (`RealService`, `FallbackService`, `MyServiceDecorator`) and a comparison using **Polly** (`PollyExample.cs`).

---

## How It Works

1. **Closed**: Calls are allowed to pass through. If failures exceed the threshold (default: 2), the circuit transitions to **Open**.
2. **Open**: Calls are blocked immediately, throwing a `CircuitBrokenException`. After a configured timeout (default: 10 seconds), the circuit enters **Half-Open**.
3. **Half-Open**: Allows a single call to test the service.
   - If the call **succeeds**, the circuit resets back to **Closed**.
   - If the call **fails**, the circuit returns to **Open** and restarts the reset timeout.

---

## Basic Usage

### 1. Simple Execution
Execute any block of code through the circuit breaker:

```csharp
using CircuitBreaker.Core;

// Configure state
var state = new CircuitBreakerState(failureThreshold: 2, resetTimeout: TimeSpan.FromSeconds(5));
ICircuitBreaker breaker = new CircuitBreaker(state);

try
{
    // Execute action
    string result = await breaker.ExecuteAsync(async () => await myService.FetchDataAsync());
    Console.WriteLine($"Result: {result}");
}
catch (CircuitBrokenException)
{
    // Handle fallback or degradation mode
    string fallbackData = await fallbackService.FetchDataAsync();
}
```

### 2. Service Decorator with Dependency Injection
Register the components in your `.NET` DI container (`Microsoft.Extensions.DependencyInjection`):

```csharp
services.AddSingleton<CircuitBreakerState>(provider => new CircuitBreakerState(
    failureThreshold: 2,
    resetTimeout: TimeSpan.FromSeconds(10)
));
services.AddSingleton<ICircuitBreaker, CircuitBreaker.Core.CircuitBreaker>();

// Decorator setup
services.AddTransient<IMyService>(provider =>
{
    var breaker = provider.GetRequiredService<ICircuitBreaker>();
    var real = new RealService();
    return new MyServiceDecorator(real, breaker);
});
```

---

## Getting Started

### Prerequisites
- .NET 10.0 SDK or higher.

### Build and Run Demo
1. Build the entire solution:
   ```bash
   dotnet build src/CircuitBreaker.slnx
   ```
2. Run the sample console application to observe the circuit state transitions:
   ```bash
   dotnet run --project src/CircuitBreaker.Sample/CircuitBreaker.Sample.csproj
   ```

### Create NuGet Package
To generate the `.nupkg` file for distribution:
```bash
dotnet pack src/CircuitBreaker.Core/CircuitBreaker.Core.csproj -c Release -o ./dist
```
The packaged library will be available in the `./dist` folder.
