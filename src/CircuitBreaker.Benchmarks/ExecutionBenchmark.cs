using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using CircuitBreaker.Core;
using Polly;

namespace CircuitBreaker.Benchmarks;

[MemoryDiagnoser]
public class ExecutionBenchmark
{
    private ICircuitBreaker _circuitBreaker;

    [GlobalSetup]
    public void Setup()
    {
        var options = new CircuitBreakerOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(5)
        };
        _circuitBreaker = CircuitBreakerFactory.Create(options);
    }

    [Benchmark(Baseline = true)]
    public async Task DirectExecution()
    {
        await DoWorkAsync();
    }

    [Benchmark]
    public async Task CircuitBreakerExecution()
    {
        await _circuitBreaker.ExecuteAsync(async () => await DoWorkAsync());
    }

    private Task DoWorkAsync()
    {
        // Simulate a very fast synchronous operation disguised as async
        return Task.CompletedTask;
    }
}
