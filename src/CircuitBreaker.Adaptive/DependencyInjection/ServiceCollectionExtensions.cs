using CircuitBreaker.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CircuitBreaker.Adaptive.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AdaptiveCircuitBreakerDecorator"/> as singleton <see cref="ICircuitBreaker"/>.
    /// Disposes the decorator when the service provider is disposed.
    /// </summary>
    public static IServiceCollection AddAdaptiveCircuitBreaker(
        this IServiceCollection services,
        CircuitBreakerOptions circuitOptions,
        AdaptiveTrafficControlOptions? adaptiveOptions = null,
        string resourceName = "Default")
    {
        services.TryAddSingleton<AdaptiveCircuitBreakerDecorator>(sp =>
        {
            var logger = sp.GetService<ILogger<AdaptiveCircuitBreakerDecorator>>();
            return AdaptiveCircuitBreakerFactory.Create(
                circuitOptions,
                adaptiveOptions,
                resourceName,
                logger);
        });

        services.TryAddSingleton<ICircuitBreaker>(sp => sp.GetRequiredService<AdaptiveCircuitBreakerDecorator>());

        return services;
    }
}
