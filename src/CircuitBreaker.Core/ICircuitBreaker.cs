using System;
using System.Threading;
using System.Threading.Tasks;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Defines a generic, simplified interface to execute operations protected by a Circuit Breaker.
    /// </summary>
    public interface ICircuitBreaker
    {
        /// <summary>
        /// Gets the current state of the circuit breaker.
        /// </summary>
        CircuitState State { get; }

        /// <summary>
        /// Gets the friendly name of the protected resource supplied at creation time.
        /// </summary>
        string ResourceName { get; }

        /// <summary>
        /// Executes an asynchronous operation that returns a result through the circuit breaker.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="action">The asynchronous operation to execute.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="Polly.CircuitBreaker.BrokenCircuitException">Thrown when the circuit is open.</exception>
        Task<T> ExecuteAsync<T>(Func<Task<T>> action);

        /// <summary>
        /// Executes an asynchronous operation that returns a result through the circuit breaker,
        /// propagating the cancellation token provided by the resilience pipeline.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="action">The asynchronous operation to execute, receiving a <see cref="CancellationToken"/>.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="Polly.CircuitBreaker.BrokenCircuitException">Thrown when the circuit is open.</exception>
        Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an asynchronous operation that does not return a result through the circuit breaker.
        /// </summary>
        /// <param name="action">The asynchronous operation to execute.</param>
        /// <exception cref="Polly.CircuitBreaker.BrokenCircuitException">Thrown when the circuit is open.</exception>
        Task ExecuteAsync(Func<Task> action);

        /// <summary>
        /// Executes an asynchronous operation that does not return a result through the circuit breaker,
        /// propagating the cancellation token provided by the resilience pipeline.
        /// </summary>
        /// <param name="action">The asynchronous operation to execute, receiving a <see cref="CancellationToken"/>.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <exception cref="Polly.CircuitBreaker.BrokenCircuitException">Thrown when the circuit is open.</exception>
        Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
    }
}
