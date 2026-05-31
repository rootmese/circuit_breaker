using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using CircuitBreaker.Core;

namespace CircuitBreaker.Sample
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("  Circuit Breaker NuGet Package Demo  ");
            Console.WriteLine("==================================================");

            // Configure DI container
            var services = new ServiceCollection();

            // Register standard distributed in-memory cache
            services.AddDistributedMemoryCache();

            services.AddSingleton<CircuitBreakerState>(provider =>
            {
                var cache = provider.GetRequiredService<IDistributedCache>();
                return new CircuitBreakerState(
                    cache: cache,
                    resourceName: "MyService",
                    failureThreshold: 2,
                    resetTimeout: TimeSpan.FromSeconds(5) // shortened for demo purposes
                );
            });
            services.AddSingleton<ICircuitBreaker, Core.CircuitBreaker>();

            services.AddTransient<RealService>();
            services.AddTransient<FallbackService>();

            // Setup a decorator factory for IMyService
            services.AddTransient<IMyService>(provider =>
            {
                var breaker = provider.GetRequiredService<ICircuitBreaker>();
                var real = provider.GetRequiredService<RealService>();
                var fallback = provider.GetRequiredService<FallbackService>();

                // In a real application, you might want to return the fallback or the decorator
                return new MyServiceDecorator(real, breaker);
            });

            var serviceProvider = services.BuildServiceProvider();
            var service = serviceProvider.GetRequiredService<IMyService>();
            var fallback = serviceProvider.GetRequiredService<FallbackService>();

            // Simulate consecutive calls
            for (int i = 1; i <= 6; i++)
            {
                Console.WriteLine($"\n--- Call #{i} ---");
                try
                {
                    var result = await service.GetDataAsync();
                    Console.WriteLine($"[APP] Result: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[APP] Error caught: {ex.Message}");
                    
                    // Directing to fallback manually when call fails
                    var fallbackResult = await fallback.GetDataAsync();
                    Console.WriteLine($"[APP] Fallback Result: {fallbackResult}");
                }

                // Small delay to make the output readable
                await Task.Delay(500);
            }

            Console.WriteLine("\nWaiting 6 seconds for the circuit reset timeout to expire...");
            await Task.Delay(6000);

            Console.WriteLine("\n--- Call #7 (Should be Half-Open then Close on success) ---");
            try
            {
                var result = await service.GetDataAsync();
                Console.WriteLine($"[APP] Result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APP] Error caught: {ex.Message}");
            }

            Console.WriteLine("\n--- Call #8 (Should be closed and run successfully) ---");
            try
            {
                var result = await service.GetDataAsync();
                Console.WriteLine($"[APP] Result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APP] Error caught: {ex.Message}");
            }

            Console.WriteLine("\n==================================================");
            Console.WriteLine("Demo completed.");
        }
    }
}
