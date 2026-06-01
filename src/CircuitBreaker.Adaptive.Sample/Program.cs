using CircuitBreaker.Adaptive;
using CircuitBreaker.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

services.AddSingleton<AdaptiveCircuitBreakerDecorator>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<AdaptiveCircuitBreakerDecorator>>();
    return AdaptiveCircuitBreakerFactory.Create(
        new CircuitBreakerOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 4,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(8),
            OnOpened = d => Console.WriteLine($"[CB] OPEN for {d.TotalSeconds:F0}s"),
            OnClosed = () => Console.WriteLine("[CB] CLOSED"),
            OnHalfOpened = () => Console.WriteLine("[CB] HALF-OPEN")
        },
        new AdaptiveTrafficControlOptions
        {
            InitialMaxRequestsPerSecond = 20,
            InitialMaxConcurrency = 5,
            ControlLoopInterval = TimeSpan.FromMilliseconds(200)
        },
        resourceName: "PaymentAPI",
        logger: logger);
});

services.AddSingleton<ICircuitBreaker>(sp => sp.GetRequiredService<AdaptiveCircuitBreakerDecorator>());

await using var provider = services.BuildServiceProvider();
var adaptive = provider.GetRequiredService<AdaptiveCircuitBreakerDecorator>();
ICircuitBreaker breaker = adaptive;

var random = new Random(42);
Console.WriteLine("Adaptive Circuit Breaker demo (20 calls, flaky downstream)");
Console.WriteLine("============================================================\n");

for (var i = 1; i <= 20; i++)
{
    Console.WriteLine($"--- Call #{i} | CB: {breaker.State} | Health: {adaptive.CurrentHealthScore} ---");

    try
    {
        var result = await breaker.ExecuteAsync(async () =>
        {
            await Task.Delay(30);
            if (random.NextDouble() < 0.55)
            {
                throw new HttpRequestException("Downstream 503");
            }

            return "OK";
        });

        Console.WriteLine($"  Result: {result}");
    }
    catch (RateLimitExceededException ex)
    {
        Console.WriteLine($"  Throttled (rate): {ex.Message}");
    }
    catch (ConcurrencyLimitExceededException ex)
    {
        Console.WriteLine($"  Throttled (concurrency): {ex.Message}");
    }
    catch (BrokenCircuitException)
    {
        Console.WriteLine("  Blocked: circuit OPEN");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Failed: {ex.Message}");
    }

    await Task.Delay(80);
}

var telemetry = await adaptive.GetLatestTelemetryAsync();
Console.WriteLine($"\nTelemetry: {telemetry}");
Console.WriteLine($"Final — Circuit: {breaker.State}, Health: {adaptive.CurrentHealthScore}");
await adaptive.DisposeAsync();
Console.WriteLine("Done.");
