using System;
using Polly;
using Polly.CircuitBreaker;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Factory class to construct instances of <see cref="ICircuitBreaker"/> powered by Polly's advanced circuit breaker.
    /// </summary>
    public static class CircuitBreakerFactory
    {
        /// <summary>
        /// Creates a new <see cref="ICircuitBreaker"/> configured with the specified options.
        /// </summary>
        /// <param name="options">The circuit breaker configuration.</param>
        /// <param name="resourceName">A friendly name for the protected resource (used in callback context).</param>
        /// <returns>A fully configured <see cref="ICircuitBreaker"/> instance.</returns>
        public static ICircuitBreaker Create(CircuitBreakerOptions options, string resourceName = "Default")
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            CircuitBreaker? breaker = null;

            var pipeline = new ResiliencePipelineBuilder()
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = options.FailureRatio,
                    SamplingDuration = options.SamplingDuration,
                    MinimumThroughput = options.MinimumThroughput,
                    BreakDuration = options.BreakDuration,
                    OnOpened = args =>
                    {
                        breaker?.UpdateState(CircuitState.Open);
                        options.OnOpened?.Invoke(args.BreakDuration);
                        return default;
                    },
                    OnClosed = args =>
                    {
                        breaker?.UpdateState(CircuitState.Closed);
                        options.OnClosed?.Invoke();
                        return default;
                    },
                    OnHalfOpened = args =>
                    {
                        breaker?.UpdateState(CircuitState.HalfOpen);
                        options.OnHalfOpened?.Invoke();
                        return default;
                    }
                })
                .Build();

            breaker = new CircuitBreaker(pipeline);
            return breaker;
        }
    }
}
