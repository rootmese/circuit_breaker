using System;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Thread-safe state machine managing the state transitions of the Circuit Breaker.
    /// </summary>
    public class CircuitBreakerState
    {
        public enum CircuitState { Closed, Open, HalfOpen }

        public CircuitState State { get; private set; } = CircuitState.Closed;

        private int _failures = 0;
        private readonly int _failureThreshold;
        private DateTime _openedTime;
        private readonly TimeSpan _resetTimeout;
        private readonly object _lock = new object();

        public CircuitBreakerState(int failureThreshold = 2, TimeSpan? resetTimeout = null)
        {
            _failureThreshold = failureThreshold;
            _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(10);
        }

        public bool AllowCall()
        {
            lock (_lock)
            {
                if (State == CircuitState.Open)
                {
                    if (DateTime.UtcNow - _openedTime > _resetTimeout)
                    {
                        State = CircuitState.HalfOpen;
                        Console.WriteLine("[CB] Half-Open: testing service recovery");
                        return true;
                    }
                    return false;
                }
                return true;
            }
        }

        public void RecordSuccess()
        {
            lock (_lock)
            {
                _failures = 0;
                State = CircuitState.Closed;
                Console.WriteLine("[CB] Circuit closed again");
            }
        }

        public void RecordFailure()
        {
            lock (_lock)
            {
                _failures++;
                if (_failures >= _failureThreshold && State != CircuitState.Open)
                {
                    State = CircuitState.Open;
                    _openedTime = DateTime.UtcNow;
                    Console.WriteLine("[CB] Circuit is OPEN!");
                }
            }
        }
    }
}
