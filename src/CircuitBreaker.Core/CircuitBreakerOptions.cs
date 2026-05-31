using System;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Configuration options for the Polly-backed Advanced Circuit Breaker.
    /// </summary>
    public class CircuitBreakerOptions
    {
        /// <summary>
        /// The failure ratio (percentage between 0.0 and 1.0) of executions that must fail to trip the circuit.
        /// Default is 0.5 (50% failure rate).
        /// </summary>
        public double FailureRatio { get; set; } = 0.5;

        /// <summary>
        /// The sliding sampling window duration during which the failure ratio is monitored.
        /// Default is 10 seconds.
        /// </summary>
        public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The minimum volume of calls that must pass through in the sampling window before the circuit breaker can trip.
        /// Default is 8 calls.
        /// </summary>
        public int MinimumThroughput { get; set; } = 8;

        /// <summary>
        /// The duration the circuit remains open before transitioning to Half-Open to test the service.
        /// Default is 5 seconds.
        /// </summary>
        public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Optional callback invoked when the circuit transitions to <see cref="CircuitState.Open"/>.
        /// Receives the break duration as a parameter.
        /// </summary>
        public Action<TimeSpan>? OnOpened { get; set; }

        /// <summary>
        /// Optional callback invoked when the circuit transitions to <see cref="CircuitState.Closed"/>.
        /// </summary>
        public Action? OnClosed { get; set; }

        /// <summary>
        /// Optional callback invoked when the circuit transitions to <see cref="CircuitState.HalfOpen"/>.
        /// </summary>
        public Action? OnHalfOpened { get; set; }
    }
}
