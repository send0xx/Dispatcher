# Dispatcher maintenance guide

## Project intent

Dispatcher is a small CQRS library for .NET applications using dependency injection. Keep the API focused on queries, commands, notifications, handlers, and pipeline behaviors. Prefer straightforward runtime code over abstractions that do not provide a measurable benefit.

The libraries target `net8.0` and `net10.0`. Samples and benchmarks target `net10.0`. The current package version is defined centrally in `Directory.Build.props`.

## Solution structure

- `src/Dispatcher.Abstractions`: public messages, handlers, dispatcher contracts, pipeline contracts, and `Unit`.
- `src/Dispatcher`: container-neutral runtime, frozen handler registry, wrappers, and exceptions.
- `src/Dispatcher.Extensions.Microsoft.DependencyInjection`: typed, reflection-free Microsoft DI registrations.
- `src/Dispatcher.DependencyInjection`: reflection-based Microsoft DI dispatcher registration and handler scanning.
- `src/Dispatcher.SourceGeneration`: generated dispatcher implementation and handler registration.
- `samples/DependencyInjection`: reflection-based modular Minimal API with internal Orders and Stock handlers.
- `samples/NativeAot`: Native AOT Minimal API where the host composes generated handlers from referenced assemblies.
- `tests/Dispatcher.Tests`: integration tests targeting .NET 8 and .NET 10.
- `benchmarks/Dispatcher.Benchmarks`: BenchmarkDotNet latency and allocation benchmarks.
- `docs/PLAN.md`: original v1 implementation plan and design history.
- `docs/AOT.md`: proposed Native AOT and source-generation roadmap.

All library types intentionally use the `Dispatcher` namespace, even when files are organized into folders.

## Settled API decisions

- Keep only the non-generic `IRequest` marker. Do not introduce `IRequest<TResponse>`.
- `IQuery<TResponse>` and `ICommand<TResponse>` inherit from `IRequest`.
- Resultless `ICommand` inherits from `ICommand<Unit>`.
- A resultless `ICommandHandler<TCommand>` returns `ValueTask`, not `ValueTask<Unit>`.
- Command dispatch methods are named `ExecuteAsync`; query dispatch uses `QueryAsync`; notification dispatch uses `PublishAsync`.
- Keep specialized query and command handler interfaces. Do not introduce a shared public `IRequestHandler<TRequest,TResponse>` unless the public design is deliberately reconsidered.
- Use one `IPipelineBehavior<TRequest,TResponse>` contract for queries and both command shapes.
- `RequestHandlerDelegate<TResponse>` accepts only a `CancellationToken`. Behaviors invoke it as `next(cancellationToken)`.
- Keep `Unit` in its own file. Its implementation follows the value semantics used by martinothamar/Mediator.
- Keep `MessageType` naming in shared handler registration and exception types because registrations also describe notifications.
- Public abstractions are organized into the current folders. Do not consolidate them into one file.

Treat changes to these contracts as breaking API changes. Discuss and benchmark them before implementation.

## Dependency injection and lifetimes

- `AddDispatcher()` registers infrastructure only. It must not scan assemblies implicitly.
- Modules register their own handlers separately through `AddDispatcherHandlers<TMarker>()` or an assembly overload.
- Reflection scanning must include internal handler classes.
- Handler assembly registration is idempotent.
- `AddPipelineBehavior` is the single supported convenience method for behavior registration. Direct Microsoft DI registrations of `IPipelineBehavior<,>` must continue to work.
- Dispatcher is scoped by design. The registry and immutable wrappers are singleton infrastructure.
- Do not change Dispatcher to singleton while scoped handlers or behaviors are supported. A singleton Dispatcher would capture the root provider and invalidate scoped dependency resolution.
- The runtime package must not depend on Microsoft.Extensions.DependencyInjection. Its direct use of the BCL `IServiceProvider` is intentional.
- Do not add an adapter around `IServiceProvider` unless it enables a concrete container integration or source-generated capability. A thin adapter adds an allocation and interface call without improving the current runtime.

## Pipeline implementation

- Resolve behaviors for each dispatch so scoped and transient lifetimes remain correct.
- When no behavior applies, invoke the handler directly. This path should remain allocation-free for synchronously completing handlers.
- Reuse an existing indexed behavior collection when the container provides one; materialize only an arbitrary enumerable fallback.
- Invoke the outermost behavior directly instead of wrapping it in an additional closure.
- Preserve registration order: the first registered behavior is outermost.
- Preserve short-circuiting and cancellation-token replacement through `next`.
- Do not add a scoped pipeline cache. ASP.NET creates a scope per HTTP request, so chain construction is generally not amortized and the cache adds per-request objects and complexity.
- Do not cache executable pipelines in singleton wrappers. They would retain scoped behaviors or a scoped provider across requests.

Expected steady-state characteristics on .NET 10 are approximately:

- Query or command without behaviors: zero managed allocations.
- One behavior: approximately 96 B.
- Three behaviors: approximately 304 B.
- Notification with one handler: approximately 32 B from collection resolution.

Treat these numbers as regression indicators, not cross-machine performance guarantees.

## Registry and handler behavior

- Use `FrozenDictionary` for request and notification wrapper lookup.
- Queries and commands require exactly one handler.
- Duplicate query or command handlers fail when the singleton registry is created.
- Notifications allow zero or more handlers and execute sequentially in registration order.
- Dispatch uses exact concrete message types; polymorphic routing is not currently supported.
- Reflection is limited to startup registration. Dispatch must not use reflection.

## Documentation style

- Every public API must have XML documentation. `CS1591` is intentionally not suppressed, and warnings are treated as errors.
- Internal APIs do not require XML documentation.
- Write summary blocks on separate lines:

```csharp
/// <summary>
/// Dispatches a query to its registered handler.
/// </summary>
```

- Do not use a single-line `<summary>...</summary>` block.
- Do not leave an empty line at the end of a file.
- Keep explanations concise and describe observable behavior, lifetimes, parameters, returns, and relevant exceptions.

## Performance work

- Measure before and after any optimization with BenchmarkDotNet.
- Use Release builds and `MemoryDiagnoser`.
- Do not use `--job Dry` results as performance evidence; Dry runs only verify discovery and execution.
- Distinguish warmed-scope throughput from an ASP.NET-style fresh-scope-per-request scenario.
- Preserve handler and behavior lifetime semantics before accepting allocation reductions.
- Avoid mutable shared pipeline state, `AsyncLocal`, scoped objects retained by singleton caches, or optimizations that break concurrent dispatch.

Run benchmarks with:

```bash
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release
```

## Verification

For normal changes, run:

```bash
dotnet build Dispatcher.slnx -c Release
dotnet test tests/Dispatcher.Tests/Dispatcher.Tests.csproj -c Release --no-build --framework net10.0
```

Run .NET 8 tests as well when a .NET 8 runtime is installed:

```bash
dotnet test tests/Dispatcher.Tests/Dispatcher.Tests.csproj -c Release --no-build --framework net8.0
```

For package-affecting changes, pack all libraries and inspect their dependencies and XML documentation:

```bash
dotnet pack src/Dispatcher.Abstractions/Dispatcher.Abstractions.csproj -c Release -o artifacts/packages
dotnet pack src/Dispatcher/Dispatcher.csproj -c Release -o artifacts/packages
dotnet pack src/Dispatcher.Extensions.Microsoft.DependencyInjection/Dispatcher.Extensions.Microsoft.DependencyInjection.csproj -c Release -o artifacts/packages
dotnet pack src/Dispatcher.DependencyInjection/Dispatcher.DependencyInjection.csproj -c Release -o artifacts/packages
dotnet pack src/Dispatcher.SourceGeneration/Dispatcher.SourceGeneration.csproj -c Release -o artifacts/packages
```

The test suite should continue covering handler dispatch, cancellation, pipeline order, short-circuiting, resultless command adaptation through `Unit`, notification order, missing and duplicate handlers, direct DI behavior registration, transient behavior lifetime, registration idempotence, and frozen registries.

## Future AOT and source generation

- Read `docs/AOT.md` before starting AOT or generator work.
- The current reflection implementation is intentionally not trimming or NativeAOT safe.
- Preserve the separate `AddDispatcherHandlers` module seam so generated registrations can replace reflection later.
- Source generation should produce explicit handler registrations and dispatch metadata rather than changing the public command/query contracts unnecessarily.
- Internal handlers across module assemblies must remain supported.
- The generator injects its internal `GenerateDispatcherHandlersAttribute`; do not add generator-only attributes to runtime or abstractions assemblies.
- Generated modules opt in with `GenerateDispatcherHandlersAttribute` and must use a unique, valid extension method name.
- Keep generator diagnostics documented in `AnalyzerReleases.Unshipped.md` and covered by generator tests.
- Add generated/AOT support as a separate implementation path and compare it against the reflection path before replacing existing behavior.

## Repository hygiene

- Preserve unrelated user changes in a dirty worktree.
- Do not add empty lines at the end of files.
- Do not use partial classes to split implementation across files. Prefer cohesive, explicitly named types with clear responsibilities.
- Do not commit `.DS_Store`, `*.DotSettings.user`, build output, benchmark artifacts, or old package artifacts.
- `artifacts/packages` may contain packages from multiple versions; never publish with an unreviewed wildcard.
- Use `apply_patch` for intentional source edits and keep changes scoped to the requested work.
