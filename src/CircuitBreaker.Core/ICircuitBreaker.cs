using System;
using System.Threading.Tasks;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Defines a generic Circuit Breaker mechanism to execute actions with resilience.
    /// </summary>
    public interface ICircuitBreaker
    {
        /// <summary>
        /// Checks if the circuit breaker allows a call to be made.
        /// </summary>
        /// <returns>True if the call is allowed; otherwise, false.</returns>
        Task<bool> AllowCallAsync();

        /// <summary>
        /// Records a successful execution, resetting the failure count.
        /// </summary>
        Task RecordSuccessAsync();

        /// <summary>
        /// Records a failed execution, incrementing the failure count.
        /// If the failures reach the threshold, the circuit breaker opens.
        /// </summary>
        Task RecordFailureAsync();

        /// <summary>
        /// Executes an asynchronous operation that returns a result through the circuit breaker.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="action">The asynchronous operation to execute.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="CircuitBrokenException">Thrown when the circuit is open.</exception>
        Task<T> ExecuteAsync<T>(Func<Task<T>> action);

        /// <summary>
        /// Executes an asynchronous operation that does not return a result through the circuit breaker.
        /// </summary>
        /// <param name="action">The asynchronous operation to execute.</param>
        /// <exception cref="CircuitBrokenException">Thrown when the circuit is open.</exception>
        Task ExecuteAsync(Func<Task> action);
    }
}
