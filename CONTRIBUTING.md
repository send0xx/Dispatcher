# Contributing to Dispatcher

Contributions and design discussions are welcome. Dispatcher deliberately keeps its public API and runtime small, so proposed abstractions or performance optimizations should demonstrate a concrete benefit and preserve handler and behavior lifetime semantics.

## Before making changes

Review the repository guidance in [AGENTS.md](AGENTS.md) before changing public contracts, registration semantics, pipelines, or source generation. Treat changes to the public command, query, notification, handler, and pipeline contracts as breaking API changes that require deliberate design discussion.

Keep changes focused and preserve unrelated work in the repository. Every public API must have XML documentation, and warnings are treated as errors.

## Build and test

Build and test changes from the repository root:

```bash
dotnet build Dispatcher.slnx -c Release
dotnet test tests/Dispatcher.Tests/Dispatcher.Tests.csproj -c Release --no-build --framework net10.0
dotnet test tests/Dispatcher.SourceGeneration.Tests/Dispatcher.SourceGeneration.Tests.csproj -c Release --no-build --framework net10.0
```

Run the .NET 8 test target as well when a .NET 8 runtime is installed:

```bash
dotnet test tests/Dispatcher.Tests/Dispatcher.Tests.csproj -c Release --no-build --framework net8.0
```

## Performance changes

Measure performance changes with BenchmarkDotNet in a Release build:

```bash
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release
```

Do not use dry benchmark jobs as performance evidence. Preserve handler and behavior lifetime semantics, and distinguish warmed-scope throughput from a fresh-scope-per-request scenario when reporting results.

## Pull requests

Include tests for observable behavior changes and update documentation when public behavior changes. The test suite should continue to cover dispatch, cancellation, pipeline behavior, handler registration, notification order, lifetimes, and source-generated registration as applicable.

Package and release automation is documented separately in the [release guide](docs/RELEASING.md).
