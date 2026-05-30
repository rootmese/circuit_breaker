using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;

namespace CircuitBreaker.Sample
{
    public class PollyExample
    {
        public void ConfigureServices(IServiceCollection services)
        {
            var pipeline = new ResiliencePipelineBuilder()
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(5),
                    BreakDuration = TimeSpan.FromSeconds(10),
                    OnOpened = _ => { Console.WriteLine("🔌 [Polly] CIRCUIT OPENED"); return default; }
                })
                .Build();

            services.AddScoped<IMyService>(provider =>
            {
                var real = new RealService();
                return new ResilienceDecorator(real, pipeline);
            });
        }
    }

    public class ResilienceDecorator : IMyService
    {
        private readonly IMyService _realService;
        private readonly ResiliencePipeline _pipeline;

        public ResilienceDecorator(IMyService realService, ResiliencePipeline pipeline)
        {
            _realService = realService ?? throw new ArgumentNullException(nameof(realService));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        public async Task<string> GetDataAsync()
        {
            return await _pipeline.ExecuteAsync(async token => await _realService.GetDataAsync());
        }
    }
}
