namespace CircuitBreaker.Telemetry;

/// <summary>
/// Records individual execution outcomes (typically from a decorator or middleware).
/// </summary>
public interface IExecutionTelemetryRecorder
{
    void RecordExecution(bool succeeded, double latencyMs, bool isTimeout = false);
}
