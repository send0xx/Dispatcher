# Contributing to Dispatcher

Contributions and design discussions are welcome. Dispatcher deliberately keeps its public API and runtime small, so
proposed abstractions or performance optimizations should demonstrate a concrete benefit and preserve handler and
behavior lifetime semantics.

## Before making changes

Review the repository before changing public contracts, registration semantics, pipelines, or source generation. Treat
changes to the public command, query, notification, handler, and pipeline contracts as breaking API changes that require
deliberate design discussion.

Keep changes focused and preserve unrelated work in the repository. Every public API must have XML documentation, and
warnings are treated as errors.

## Build and test

The test projects run
on [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro) rather
than VSTest, selected by the `test` section of [`global.json`](global.json). Removing that file makes `dotnet test`
fail, because xUnit v3 no longer ships a VSTest adapter.

Run every test project and both target frameworks at once:

```bash
dotnet test Dispatcher.slnx -c Release
```

Build and test changes from the repository root:

```bash
dotnet build Dispatcher.slnx -c Release
dotnet test tests/Dispatcher.DependencyInjection.Tests/Dispatcher.DependencyInjection.Tests.csproj -c Release --no-build --framework net10.0
dotnet test tests/Dispatcher.Parity.Tests/Dispatcher.Parity.Tests.csproj -c Release --no-build --framework net10.0
dotnet test tests/Dispatcher.SourceGeneration.Tests/Dispatcher.SourceGeneration.Tests.csproj -c Release --no-build --framework net10.0
```

Run the .NET 8 test target as well when a .NET 8 runtime is installed:

```bash
dotnet test tests/Dispatcher.DependencyInjection.Tests/Dispatcher.DependencyInjection.Tests.csproj -c Release --no-build --framework net8.0
dotnet test tests/Dispatcher.Parity.Tests/Dispatcher.Parity.Tests.csproj -c Release --no-build --framework net8.0
dotnet test tests/Dispatcher.SourceGeneration.Tests/Dispatcher.SourceGeneration.Tests.csproj -c Release --no-build --framework net8.0
```

## Performance changes

Measure performance changes with BenchmarkDotNet in a Release build:

```bash
dotnet run --project benchmarks/Dispatcher.Benchmarks.Execution -c Release -- execution
dotnet run --project benchmarks/Dispatcher.Benchmarks.Telemetry -c Release -- telemetry
dotnet run --project benchmarks/Dispatcher.Benchmarks.Scale -c Release -- scale
```

Do not use dry benchmark jobs as performance evidence. Preserve handler and behavior lifetime semantics, and distinguish
warmed-scope throughput from a fresh-scope-per-request scenario when reporting results.

## Pull requests

Include tests for observable behavior changes and update documentation when public behavior changes. The test suite
should continue to cover dispatch, cancellation, pipeline behavior, handler registration, notification order, lifetimes,
and source-generated registration as applicable.

Package and release automation is documented separately in the [release guide](RELEASING.md).
