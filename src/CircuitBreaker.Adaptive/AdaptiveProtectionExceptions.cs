namespace CircuitBreaker.Adaptive;

public class RateLimitExceededException : Exception
{
    public RateLimitExceededException(string message) : base(message) { }
}

public class ConcurrencyLimitExceededException : Exception
{
    public ConcurrencyLimitExceededException(string message) : base(message) { }
}
