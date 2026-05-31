using System;
using System.Threading.Tasks;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Core implementation of the Circuit Breaker pattern.
    /// </summary>
    public class CircuitBreaker : ICircuitBreaker
    {
        private readonly CircuitBreakerState _state;

        public CircuitBreaker(CircuitBreakerState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public async Task<bool> AllowCallAsync() => await _state.AllowCallAsync();

        public async Task RecordSuccessAsync() => await _state.RecordSuccessAsync();

        public async Task RecordFailureAsync() => await _state.RecordFailureAsync();

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!await AllowCallAsync())
            {
                Console.WriteLine($"[CB - {_state.ResourceName}] Execution blocked (circuit is open)");
                throw new CircuitBrokenException("Circuit is open. Call blocked.");
            }

            try
            {
                var result = await action();
                await RecordSuccessAsync();
                return result;
            }
            catch (Exception)
            {
                await RecordFailureAsync();
                throw;
            }
        }

        public async Task ExecuteAsync(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!await AllowCallAsync())
            {
                Console.WriteLine($"[CB - {_state.ResourceName}] Execution blocked (circuit is open)");
                throw new CircuitBrokenException("Circuit is open. Call blocked.");
            }

            try
            {
                await action();
                await RecordSuccessAsync();
            }
            catch (Exception)
            {
                await RecordFailureAsync();
                throw;
            }
        }
    }
}
