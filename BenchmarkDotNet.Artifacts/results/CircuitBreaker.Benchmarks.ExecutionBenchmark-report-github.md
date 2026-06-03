```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8457)
Unknown processor
.NET SDK 10.0.203
  [Host]     : .NET 9.0.16 (9.0.1626.22923), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.16 (9.0.1626.22923), X64 RyuJIT AVX2


```
| Method                  | Mean       | Error      | StdDev    | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|-----------:|----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| DirectExecution         |   9.161 ns |  0.6946 ns |  2.015 ns |   8.201 ns |  1.04 |    0.30 |      - |         - |          NA |
| CircuitBreakerExecution | 487.851 ns | 34.2455 ns | 99.896 ns | 457.964 ns | 55.37 |   15.08 | 0.0362 |     152 B |          NA |
