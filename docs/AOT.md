# Native AOT and source-generation design

## Current status

The Native AOT implementation is complete:

- typed manual registration exists for queries, both command shapes, and notifications;
- typed registrations construct closed wrappers without `MakeGenericType` or `Activator.CreateInstance`;
- reflection registration remains available and has trimming and dynamic-code compatibility annotations;
- all runtime library projects enable AOT compatibility analysis;
- `samples/NativeAot/ModuleOwned/Dispatcher.NativeAotSample` publishes and runs as a warning-free native executable using internal handlers, typed closed FluentValidation behavior registration, and source-generated JSON metadata.
- `Dispatcher.SourceGeneration` emits deterministic handler registrations and a concrete dispatcher for internal handlers;
- generator diagnostics report invalid method names, duplicate or missing request handlers, unsupported open generic handlers, and handlers that cannot be activated by DI;
- the module-owned Native AOT sample consumes a fully generated dispatcher behind its module entry point;
- the host-owned Native AOT sample composes two generated modules into one host-generated dispatcher;
- both Native AOT samples reference the source-generation implementation without the reflection implementation package;

Open generic pipeline behaviors are explicitly closed through the typed `AddPipelineBehavior<TRequest, TResponse, TBehavior>` API. This keeps behavior applicability and ordering visible at the composition root while remaining fully Native AOT safe. Automatic behavior closure is not part of the current generator scope.

## Objective

Add Native AOT and trimming support without replacing the existing reflection implementation or unnecessarily changing the public query, command, notification, and pipeline contracts.

Native AOT performs trimming and cannot safely support unbounded runtime reflection or runtime construction of unknown generic types. The generated path must avoid assembly scanning, `MakeGenericType`, and `Activator.CreateInstance`.

## Architectural direction

Treat reflection and source generation as separate implementation choices:

```text
Dispatcher.Extensions.Microsoft.DependencyInjection ── reflection scanner + runtime wrappers
Dispatcher.SourceGeneration ───────────────────────── generated registrations + dispatcher
```

The reflection path remains convenient for normal applications. The generated path emits handler
registrations in each module and one internal `Dispatcher` in the host assembly. Applications select one implementation package;
samples must not combine both.

## Phase 1: typed manual registration

Introduce AOT-safe typed registration methods before building a generator:

```csharp
services.AddDispatcher();

services.AddQueryHandler<
    GetOrderQuery,
    Order?,
    GetOrderQueryHandler>();

services.AddCommandHandler<
    CreateOrderCommand,
    Guid,
    CreateOrderCommandHandler>();

services.AddCommandHandler<
    ClearOrdersCommand,
    ClearOrdersCommandHandler>();

services.AddNotificationHandler<
    OrderCreated,
    ReserveStockHandler>();
```

Each method should register the handler with DI and add a typed wrapper registration without runtime generic construction. This manual path provides an independently testable AOT foundation. The source generator will later emit calls to these methods.

## Typed registry builder

Introduce a builder that constructs closed wrappers through generic code visible to the compiler and trimmer:

```csharp
public void AddQuery<TQuery, TResponse, THandler>()
    where TQuery : IQuery<TResponse>
    where THandler : IQueryHandler<TQuery, TResponse>
{
    Add(
        typeof(TQuery),
        new QueryHandlerWrapper<TQuery, TResponse>(),
        typeof(THandler));
}
```

Equivalent methods are needed for result-bearing commands, resultless commands, and notifications. The builder should retain existing duplicate-handler validation and produce the required `FrozenDictionary` instances when registration completes.

The reflection scanner may continue feeding runtime registrations into the same builder. The generated path must use typed methods exclusively.

## Phase 2: generator package

Create a separate analyzer package:

```text
Dispatcher.Abstractions
Dispatcher
Dispatcher.Extensions.Microsoft.DependencyInjection
Dispatcher.SourceGeneration
```

`Dispatcher.SourceGeneration` should use an incremental Roslyn generator. Keeping it separate prevents reflection-only consumers from acquiring Roslyn dependencies and allows generator releases to evolve independently.

The generator should run in every module containing handlers. Generated code is compiled into that module and can therefore reference its internal handler types directly.

Modules name their generated handler-registration method:

```csharp
[assembly: GenerateDispatcherHandlers("AddOrdersHandlers")]
```

The host requests the single dispatcher and composes module registrations:

```csharp
[assembly: GenerateDispatcher("AddDispatcher")]
```

```csharp
services.AddOrdersHandlers().AddStockHandlers().AddDispatcher();
```

Module-generated code can reference internal handlers because it is compiled into the module.
The host-generated dispatcher references only public message and handler contracts while building
routes across opted-in referenced modules. It does not use call-site interception or partial classes.

## Reflection fallback

Keep the current API for non-AOT applications:

```csharp
services.AddDispatcherHandlers<OrdersModule>();
```

Once a generated path exists, annotate reflection registration with clear compatibility warnings:

```csharp
[RequiresUnreferencedCode(
    "Reflection-based handler discovery is not trimming safe. " +
    "Use generated handler registration.")]
[RequiresDynamicCode(
    "Reflection-based wrapper construction requires runtime generic creation.")]
```

Do not suppress trimming or AOT warnings globally.

## Pipeline behaviors

Open generic behaviors require explicit closure because an AOT compiler must see every required closed generic instantiation.

Applications register each applicable closed behavior explicitly:

```csharp
services.AddScoped<
    IPipelineBehavior<CreateOrderCommand, Guid>,
    ValidationBehavior<CreateOrderCommand, Guid>>();
```

The generator supports:

- closed query and command types;
- closed handler implementations;
- resultless commands represented as `Unit` in the pipeline;
- deterministic handler registration;
- compile-time handler diagnostics.

Behavior registration remains an application concern. The typed registration API validates generic constraints at compile time, and Microsoft DI preserves registration order. Automatically generating closed behavior registrations may be reconsidered later as an ergonomic feature, but it is not required for Native AOT correctness.

Open generic commands and handlers may remain unsupported initially. Emit an actionable diagnostic instead of generating unsafe runtime fallback code.

Do not reintroduce scoped or singleton executable-pipeline caching. Generated registration must preserve scoped and transient behavior semantics.

## Generator diagnostics

Report compile-time errors for:

- a query or command without a handler;
- multiple handlers for one query or command;
- request and response type mismatches;
- invalid generic constraints;
- unsupported open generic handlers;
- handler types that cannot be registered or constructed by DI.

Consider warnings for:

- notifications without handlers;
- reflection and generated registration mixed for the same module;
- a behavior that cannot apply to any known request;
- duplicate generated module registration.

Compile-time diagnostics are a primary benefit of the generated path, not merely a side effect of AOT support.

## AOT sample and validation

Maintain two dedicated applications:

```text
samples/NativeAot/ModuleOwned/Dispatcher.NativeAotSample
samples/NativeAot/HostOwned/Dispatcher.NativeAotHostSample
```

Together they demonstrate:

- `WebApplication.CreateSlimBuilder`;
- a fully generated dispatcher behind a module-owned composition method;
- a generated dispatcher from a referenced handler assembly called directly by the host;
- internal handlers in separate module assemblies;
- queries and both command shapes;
- multiple notification handlers;
- a FluentValidation command behavior;
- source-generated `System.Text.Json` metadata.

CI should publish a native executable, start it, and exercise its endpoints:

```bash
dotnet publish samples/NativeAot/ModuleOwned/Dispatcher.NativeAotSample \
  -c Release \
  -r linux-x64 \
  /p:PublishAot=true

dotnet publish samples/NativeAot/HostOwned/Dispatcher.NativeAotHostSample \
  -c Release \
  -r linux-x64 \
  /p:PublishAot=true
```

Success criteria include zero unexpected trimming/AOT warnings and behavior matching the JIT application. Compilation alone is insufficient.

## Package compatibility settings

The runtime library projects enable `IsAotCompatible`, which activates the trimming, single-file, and AOT compatibility analyzers. Reflection entry points remain annotated as incompatible, while typed and generated registration provide the supported Native AOT path.

The relevant MSBuild settings are:

```xml
<IsTrimmable>true</IsTrimmable>
<IsAotCompatible>true</IsAotCompatible>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
<EnableAotAnalyzer>true</EnableAotAnalyzer>
```

Native samples must continue publishing without unexpected trimming or AOT warnings.

## Delivery sequence

1. Add typed manual handler and wrapper registration. **Complete.**
2. Publish and execute an AOT sample using only manual registration. **Complete.**
3. Add the incremental generator that emits the same registration calls. **Complete.**
4. Add compile-time handler diagnostics. **Complete.**
5. Keep pipeline behaviors explicitly closed through the typed registration API. **Complete.**
6. Add native publish and endpoint smoke tests to CI. **Pending.**
7. Benchmark reflection startup against generated startup and measure application size. **Pending.**
8. Add an optional fully generated dispatcher implementation. **Complete.**

The first AOT milestone is successful warning-free native publication with correct behavior.
