# Dispatcher benchmarks

The benchmark suite separates runtime execution, telemetry, and application-scale startup and generation measurements.
Reflection and source-generated cases use the same CLR workload types and deterministic inputs.

| Project                           | Measures                                                                                              |
|-----------------------------------|-------------------------------------------------------------------------------------------------------|
| `Dispatcher.Benchmarks.Execution` | Query, command, notification, pipeline-depth, and notification fan-out hot paths                      |
| `Dispatcher.Benchmarks.Telemetry` | Metrics and tracing with and without listeners, for successful and failed dispatch                    |
| `Dispatcher.Benchmarks.Scale`     | Reflection scanning/startup and cold or incremental source generation over temporary modular fixtures |

Run profiles from the repository root:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks.Execution -c Release -- execution
dotnet run --project benchmarks/Dispatcher.Benchmarks.Telemetry -c Release -- telemetry
dotnet run --project benchmarks/Dispatcher.Benchmarks.Scale -c Release -- scale
```

`quick` selects representative execution and telemetry groups and restricts scale measurements to the 100-message
fixture. `full` selects every group in a project. A complete run consists of the three `full` commands:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks.Execution -c Release -- full
dotnet run --project benchmarks/Dispatcher.Benchmarks.Telemetry -c Release -- full
dotnet run --project benchmarks/Dispatcher.Benchmarks.Scale -c Release -- full
```

Use BenchmarkDotNet filters after the profile while iterating:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks.Execution -c Release -- execution --filter '*PipelineDepth*'
dotnet run --project benchmarks/Dispatcher.Benchmarks.Scale -c Release -- scale --filter '*ColdHost*'
```

The scale executable generates source in memory only after it starts. Global setup compiles physical assemblies into a
unique temporary directory, loads them through a collectible context, validates reflection/generated parity, and removes
the directory during cleanup. Normal solution builds never generate or compile the 100-, 1,000-, or 5,000-message
corpus. Validate the small fixture without running BenchmarkDotNet:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks.Execution -c Release -- validate
dotnet run --project benchmarks/Dispatcher.Benchmarks.Telemetry -c Release -- validate
dotnet run --project benchmarks/Dispatcher.Benchmarks.Scale -c Release -- validate
```

Measure end-to-end clean and incremental `dotnet build` time in an isolated temporary workspace separately from
BenchmarkDotNet:

```shell
dotnet run --project benchmarks/Dispatcher.Benchmarks.Scale -c Release -- build-timing small
dotnet run --project benchmarks/Dispatcher.Benchmarks.Scale -c Release -- build-timing medium
dotnet run --project benchmarks/Dispatcher.Benchmarks.Scale -c Release -- build-timing large
```

Use Release builds on an idle machine without a debugger. BenchmarkDotNet reports the runtime, SDK, architecture,
operating system, hardware, absolute time, and managed allocations. Interpret scale generation separately from final
assembly emission. Dry jobs are useful only for discovery and setup validation; do not use them as performance evidence.
Scale scanning, startup, generation, and emission run once per measurement iteration so each observation represents one
operation instead of a GC-sensitive batch. Sampled dispatch retains BenchmarkDotNet's adaptive throughput batching.

Source-generation method boundaries are explicit:

| Method                            | Generator driver                               | Compilation/emission                                     |
|-----------------------------------|------------------------------------------------|----------------------------------------------------------|
| `ColdModuleHandlerRegistration`   | Constructed in the operation                   | Input module already parsed and compiled; no emit        |
| `ColdHostDispatcherGeneration`    | Constructed in the operation                   | Input host and module metadata already compiled; no emit |
| `TotalColdGeneration`             | One new driver per module and host             | Fixture compilation and disk output excluded             |
| `CachedIncrementalGeneration`     | Reuses a warmed driver                         | No source change; no emit                                |
| `IncrementalAfterMessageChange`   | Reuses the same warmed baseline                | One host message change; no emit                         |
| `IncrementalAfterModuleReference` | Reuses a driver warmed without the last module | Adds the final metadata reference; no emit               |
| `EmitUpdatedHostAssembly`         | Reuses the changed-source baseline             | Generation and final in-memory emit included             |

The expected runtime is 8–12 minutes for `quick`, 10–15 minutes for `execution`, 8–12 minutes for `telemetry`, 25–45
minutes for `scale`, and 50–65 minutes for all three `full` commands on the reference Apple M5 Pro. Keep only curated
reference tables here; do not commit `BenchmarkDotNet.Artifacts`.
