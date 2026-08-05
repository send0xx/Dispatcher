# Dispatcher benchmarks

The benchmarks measure dispatch latency and managed allocations for queries, commands,
notifications, and pipelines with zero, one, or three behaviors.

Run all benchmarks from the repository root:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release
```

Run one benchmark class while iterating:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release -- --filter '*PipelineBenchmarks*'
```

BenchmarkDotNet writes detailed reports to `BenchmarkDotNet.Artifacts/`. Run benchmarks
on an otherwise idle machine without a debugger attached for representative results.
