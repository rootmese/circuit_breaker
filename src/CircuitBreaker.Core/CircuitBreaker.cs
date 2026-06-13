using System;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Encapsulates Polly's ResiliencePipeline to implement the Circuit Breaker pattern.
    /// </summary>
    public class CircuitBreaker : ICircuitBreaker
    {
        private readonly ResiliencePipeline _pipeline;
        private volatile int _state; // backing field for thread-safe reads

        public CircuitBreaker(ResiliencePipeline pipeline, string resourceName)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            ResourceName = string.IsNullOrWhiteSpace(resourceName)
                ? "Default"
                : resourceName;
        }

        /// <inheritdoc />
        public CircuitState State => (CircuitState)_state;

        /// <inheritdoc />
        public string ResourceName { get; }

        /// <summary>
        /// Updates the circuit state. Called internally by <see cref="CircuitBreakerFactory"/> event callbacks.
        /// </summary>
        internal void UpdateState(CircuitState state) => _state = (int)state;

        /// <inheritdoc />
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            return await _pipeline.ExecuteAsync(async _ => await action());
        }

        /// <inheritdoc />
        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            return await _pipeline.ExecuteAsync(async token => await action(token), cancellationToken);
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            await _pipeline.ExecuteAsync(async _ => await action());
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            await _pipeline.ExecuteAsync(async token => await action(token), cancellationToken);
        }
    }
}
