namespace CircuitBreaker.Core
{
    /// <summary>
    /// Represents the current state of the circuit breaker.
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// The circuit is closed. Requests flow normally.
        /// </summary>
        Closed,

        /// <summary>
        /// The circuit is open. All requests are blocked.
        /// </summary>
        Open,

        /// <summary>
        /// The circuit is half-open. A single test request is allowed through.
        /// </summary>
        HalfOpen
    }
}
