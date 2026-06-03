```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8457)
Unknown processor
.NET SDK 10.0.203
  [Host]     : .NET 9.0.16 (9.0.1626.22923), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.16 (9.0.1626.22923), X64 RyuJIT AVX2


```
| Method                  | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| DirectExecution         |   8.849 ns | 0.2062 ns | 0.3271 ns |  1.00 |    0.05 |      - |         - |          NA |
| CircuitBreakerExecution | 425.368 ns | 6.5498 ns | 5.8062 ns | 48.14 |    1.90 | 0.0362 |     152 B |          NA |
