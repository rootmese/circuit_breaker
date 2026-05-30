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

        public bool AllowCall() => _state.AllowCall();

        public void RecordSuccess() => _state.RecordSuccess();

        public void RecordFailure() => _state.RecordFailure();

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!AllowCall())
            {
                Console.WriteLine("[CB] Execution blocked (circuit is open)");
                throw new CircuitBrokenException("Circuit is open. Call blocked.");
            }

            try
            {
                var result = await action();
                RecordSuccess();
                return result;
            }
            catch (Exception)
            {
                RecordFailure();
                throw;
            }
        }

        public async Task ExecuteAsync(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!AllowCall())
            {
                Console.WriteLine("[CB] Execution blocked (circuit is open)");
                throw new CircuitBrokenException("Circuit is open. Call blocked.");
            }

            try
            {
                await action();
                RecordSuccess();
            }
            catch (Exception)
            {
                RecordFailure();
                throw;
            }
        }
    }
}
