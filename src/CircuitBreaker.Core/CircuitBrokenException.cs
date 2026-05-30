using System;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Exception thrown when the circuit breaker is open and blocks further executions.
    /// </summary>
    public class CircuitBrokenException : Exception
    {
        public CircuitBrokenException() : base("The circuit is open and calls are blocked.") { }

        public CircuitBrokenException(string message) : base(message) { }

        public CircuitBrokenException(string message, Exception innerException) : base(message, innerException) { }
    }
}
