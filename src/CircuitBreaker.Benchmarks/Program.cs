using BenchmarkDotNet.Running;

namespace CircuitBreaker.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<ExecutionBenchmark>();
    }
}
