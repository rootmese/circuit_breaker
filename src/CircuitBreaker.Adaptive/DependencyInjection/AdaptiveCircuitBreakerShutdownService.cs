using Microsoft.Extensions.Hosting;

namespace CircuitBreaker.Adaptive.DependencyInjection;

internal sealed class AdaptiveCircuitBreakerShutdownService : IHostedService
{
    private readonly AdaptiveCircuitBreakerDecorator _decorator;

    public AdaptiveCircuitBreakerShutdownService(AdaptiveCircuitBreakerDecorator decorator)
    {
        _decorator = decorator;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) =>
        _decorator.DisposeAsync().AsTask();
}
