namespace CircuitBreaker.Adaptive;

internal static class HealthScoreTrafficTiers
{
    public static int MapToRateLimit(HealthScore score, int baselinePermitsPerSecond) =>
        ScaleLimit(score, baselinePermitsPerSecond);

    public static int MapToConcurrency(HealthScore score, int baselineMaxConcurrency) =>
        ScaleLimit(score, baselineMaxConcurrency);

    private static int ScaleLimit(HealthScore score, int baseline)
    {
        if (baseline <= 0)
        {
            return 0;
        }

        var factor = score.Value switch
        {
            > 0.9 => 1.0,
            > 0.7 => 0.75,
            > 0.4 => 0.5,
            > 0.2 => 0.25,
            _ => 0.0
        };

        return (int)Math.Max(0, Math.Round(baseline * factor));
    }
}
