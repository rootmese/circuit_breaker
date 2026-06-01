namespace CircuitBreaker.Adaptive;

internal static class HealthScoreTrafficTiers
{
    public static int MapToRateLimit(HealthScore score) => score.Value switch
    {
        > 0.9 => 1000,
        > 0.7 => 750,
        > 0.4 => 500,
        > 0.2 => 250,
        _ => 0
    };

    public static int MapToConcurrency(HealthScore score) => score.Value switch
    {
        > 0.9 => 100,
        > 0.7 => 50,
        > 0.4 => 20,
        > 0.2 => 5,
        _ => 0
    };
}
