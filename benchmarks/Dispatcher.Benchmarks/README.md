# Dispatcher benchmarks

The benchmarks measure dispatch latency and managed allocations for queries, commands,
notifications, pipelines with zero, one, or three behaviors, and telemetry with and
without active listeners.

Run all benchmarks from the repository root:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release
```

Run one benchmark class while iterating:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release -- --filter '*PipelineBenchmarks*'
```

Compare the reflection and source-generated implementations under the same runtime:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release -- --filter '*Implementation*'
```

Measure the disabled path and enabled metrics/tracing paths:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release -- --filter '*TelemetryBenchmarks*'
```

Reference results on .NET 10.0.10, Apple M5 Pro, Arm64:

| Mode | Mean | Allocated |
| --- | ---: | ---: |
| Disabled | 21.15 ns | 0 B |
| Metrics, no listener | 23.19 ns | 0 B |
| Tracing, no listener | 24.79 ns | 0 B |
| Metrics and tracing, no listeners | 24.73 ns | 0 B |
| Metrics with listener | 44.93 ns | 0 B |
| Tracing with listener | 119.66 ns | 608 B |
| Metrics and tracing with listeners | 141.17 ns | 608 B |

These numbers are regression indicators for this machine, not cross-machine guarantees.

BenchmarkDotNet writes detailed reports to `BenchmarkDotNet.Artifacts/`. Run benchmarks
on an otherwise idle machine without a debugger attached for representative results.
