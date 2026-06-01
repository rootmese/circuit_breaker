using CircuitBreaker.Core;
using CircuitBreaker.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CircuitBreaker.Adaptive;

/// <summary>
/// Background loop: telemetry → health score → actuator adjustments.
/// </summary>
public sealed class AdaptiveTrafficController : IAsyncDisposable
{
    private readonly ITelemetryProvider _telemetry;
    private readonly HealthScoreCalculator _healthCalculator;
    private readonly IReadOnlyList<IAdaptiveController> _actuators;
    private readonly ICircuitBreaker? _circuitBreaker;
    private readonly ILogger _logger;
    private readonly TimeSpan _controlLoopInterval;
    private readonly double _scoreSmoothingFactor;
    private readonly double _scoreChangeThreshold;
    private readonly double _suddenDegradationDelta;
    private readonly HealthScore _emergencyScore;
    private readonly CancellationTokenSource _cts = new();
    private Task? _controlLoopTask;
    private HealthScore _currentScore = HealthScore.Healthy();
    private bool _disposed;

    public AdaptiveTrafficController(
        ITelemetryProvider telemetry,
        IEnumerable<IAdaptiveController> actuators,
        HealthScoreCalculator? healthCalculator = null,
        ICircuitBreaker? circuitBreaker = null,
        AdaptiveTrafficControlOptions? options = null,
        ILogger? logger = null)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _actuators = actuators?.ToList() ?? throw new ArgumentNullException(nameof(actuators));
        _healthCalculator = healthCalculator ?? new HealthScoreCalculator();
        _circuitBreaker = circuitBreaker;
        _logger = logger ?? NullLogger.Instance;

        var opts = options ?? new AdaptiveTrafficControlOptions();
        _controlLoopInterval = opts.ControlLoopInterval;
        _scoreSmoothingFactor = Math.Clamp(opts.ScoreSmoothingFactor, 0.0, 1.0);
        _scoreChangeThreshold = Math.Max(0.0, opts.ScoreChangeThreshold);
        _suddenDegradationDelta = opts.SuddenDegradationDelta;
        _emergencyScore = opts.EmergencyHealthScore;
    }

    public HealthScore CurrentHealthScore => _currentScore;

    public void Start()
    {
        if (_controlLoopTask is { IsCompleted: false })
        {
            return;
        }

        _controlLoopTask = Task.Run(RunControlLoopAsync);
        _logger.LogInformation(
            "Adaptive traffic controller started (interval {Interval}ms)",
            _controlLoopInterval.TotalMilliseconds);
    }

    public async Task StopAsync()
    {
        await _cts.CancelAsync();
        if (_controlLoopTask is not null)
        {
            try
            {
                await _controlLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _logger.LogInformation("Adaptive traffic controller stopped");
    }

    public Task<TelemetrySnapshot> GetLatestTelemetryAsync(CancellationToken cancellationToken = default) =>
        _telemetry.CollectAsync(cancellationToken);

    private async Task RunControlLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _telemetry.CollectAsync(_cts.Token).ConfigureAwait(false);
                var newScore = _healthCalculator.Calculate(snapshot);
                var smoothedScore = SmoothScore(_currentScore, newScore);

                if (Math.Abs(smoothedScore.Value - _currentScore.Value) < _scoreChangeThreshold)
                {
                    await Task.Delay(_controlLoopInterval, _cts.Token).ConfigureAwait(false);
                    continue;
                }

                if (smoothedScore.Value < _currentScore.Value - _suddenDegradationDelta)
                {
                    _logger.LogWarning(
                        "Sudden degradation: {Old:F2} → {New:F2}",
                        _currentScore.Value, smoothedScore.Value);
                    await ApplyEmergencyMeasuresAsync(_cts.Token).ConfigureAwait(false);
                }

                LogHealthTransitions(smoothedScore);

                foreach (var actuator in _actuators)
                {
                    try
                    {
                        await actuator.ApplyControlAsync(smoothedScore, _cts.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Actuator {Actuator} failed", actuator.Name);
                    }
                }

                _currentScore = smoothedScore;
                await Task.Delay(_controlLoopInterval, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Adaptive control loop error");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void LogHealthTransitions(HealthScore newScore)
    {
        if (newScore.Value < 0.8 && _currentScore.Value >= 0.8)
        {
            _logger.LogWarning("Service degraded (score {Score:F2})", newScore.Value);
        }
        else if (newScore.Value < 0.4 && _currentScore.Value >= 0.4)
        {
            _logger.LogError("Service critical (score {Score:F2})", newScore.Value);
        }
        else if (newScore.Value >= 0.8 && _currentScore.Value < 0.8)
        {
            _logger.LogInformation("Service recovered (score {Score:F2})", newScore.Value);
        }
    }

    private HealthScore SmoothScore(HealthScore current, HealthScore next)
    {
        var delta = next.Value - current.Value;
        if (Math.Abs(delta) < _scoreChangeThreshold)
        {
            return current;
        }

        return new HealthScore(current.Value + delta * _scoreSmoothingFactor);
    }

    private async Task ApplyEmergencyMeasuresAsync(CancellationToken cancellationToken)
    {
        foreach (var actuator in _actuators)
        {
            try
            {
                await actuator.ApplyControlAsync(_emergencyScore, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Emergency actuator {Actuator} failed", actuator.Name);
            }
        }

        if (_circuitBreaker is not null && _currentScore.Value < 0.1)
        {
            _logger.LogCritical(
                "Health critical ({Score:F2}); circuit state is {State} (Polly remains ultimate protection)",
                _currentScore.Value,
                _circuitBreaker.State);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _cts.Dispose();

        foreach (var actuator in _actuators.OfType<IDisposable>())
        {
            actuator.Dispose();
        }

        _disposed = true;
    }
}
