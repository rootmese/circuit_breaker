using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using CircuitBreaker.Core;
using Polly.CircuitBreaker;

namespace CircuitBreaker.Sample
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("  Circuit Breaker NuGet Package Demo  ");
            Console.WriteLine("  Powered by Polly v8 Advanced Circuit Breaker");
            Console.WriteLine("==================================================");

            // Configure DI container
            var services = new ServiceCollection();

            // Register circuit breaker using the factory with sliding window options
            // and observable callbacks (instead of hardcoded Console.WriteLine in the lib)
            services.AddSingleton<ICircuitBreaker>(provider =>
                CircuitBreakerFactory.Create(
                    new CircuitBreakerOptions
                    {
                        FailureRatio = 0.5,
                        SamplingDuration = TimeSpan.FromSeconds(10),
                        MinimumThroughput = 2,
                        BreakDuration = TimeSpan.FromSeconds(5),

                        // Consumer-defined callbacks for observability
                        OnOpened = breakDuration =>
                            Console.WriteLine($"\u26a1 [CB] Circuit OPENED for {breakDuration.TotalSeconds}s due to failures."),
                        OnClosed = () =>
                            Console.WriteLine($"\u2705 [CB] Circuit CLOSED. System healthy."),
                        OnHalfOpened = () =>
                            Console.WriteLine($"\U0001f50d [CB] Circuit HALF-OPEN. Testing recovery...")
                    },
                    resourceName: "MyService"
                )
            );

            services.AddTransient<RealService>();
            services.AddTransient<FallbackService>();

            // Setup a decorator factory for IMyService
            services.AddTransient<IMyService>(provider =>
            {
                var breaker = provider.GetRequiredService<ICircuitBreaker>();
                var real = provider.GetRequiredService<RealService>();
                return new MyServiceDecorator(real, breaker);
            });

            var serviceProvider = services.BuildServiceProvider();
            var service = serviceProvider.GetRequiredService<IMyService>();
            var fallback = serviceProvider.GetRequiredService<FallbackService>();
            var breaker = serviceProvider.GetRequiredService<ICircuitBreaker>();

            // Simulate consecutive calls
            for (int i = 1; i <= 6; i++)
            {
                Console.WriteLine($"\n--- Call #{i} | Circuit State: {breaker.State} ---");
                try
                {
                    var result = await service.GetDataAsync();
                    Console.WriteLine($"[APP] Result: {result}");
                }
                catch (BrokenCircuitException)
                {
                    Console.WriteLine($"[APP] Circuit is OPEN — request blocked.");

                    // Directing to fallback when circuit is broken
                    var fallbackResult = await fallback.GetDataAsync();
                    Console.WriteLine($"[APP] Fallback Result: {fallbackResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[APP] Service error: {ex.Message}");

                    // Directing to fallback manually when call fails
                    var fallbackResult = await fallback.GetDataAsync();
                    Console.WriteLine($"[APP] Fallback Result: {fallbackResult}");
                }

                // Small delay to make the output readable
                await Task.Delay(500);
            }

            Console.WriteLine($"\n[STATE] Circuit is: {breaker.State}");
            Console.WriteLine("Waiting 6 seconds for the circuit reset timeout to expire...");
            await Task.Delay(6000);

            Console.WriteLine($"\n--- Call #7 (Half-Open test) | Circuit State: {breaker.State} ---");
            try
            {
                var result = await service.GetDataAsync();
                Console.WriteLine($"[APP] Result: {result}");
            }
            catch (BrokenCircuitException)
            {
                Console.WriteLine($"[APP] Circuit still OPEN.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APP] Service error: {ex.Message}");
            }

            Console.WriteLine($"\n--- Call #8 (Should be Closed) | Circuit State: {breaker.State} ---");
            try
            {
                var result = await service.GetDataAsync();
                Console.WriteLine($"[APP] Result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APP] Error caught: {ex.Message}");
            }

            Console.WriteLine($"\n[STATE] Final circuit state: {breaker.State}");
            Console.WriteLine("==================================================");
            Console.WriteLine("Demo completed.");
        }
    }
}
