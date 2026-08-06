# Native AOT and source-generation design

## Current status

The first two delivery milestones are implemented:

- typed manual registration exists for queries, both command shapes, and notifications;
- typed registrations construct closed wrappers without `MakeGenericType` or `Activator.CreateInstance`;
- reflection registration remains available and has trimming and dynamic-code compatibility annotations;
- all library projects enable AOT compatibility analysis;
- `samples/Dispatcher.NativeAotSample` publishes and runs as a warning-free native executable using internal handlers, typed closed FluentValidation behavior registration, and source-generated JSON metadata.

The next milestone is the incremental source generator. It should emit the same typed registration calls already proven by the native sample.

## Objective

Add Native AOT and trimming support without replacing the existing reflection implementation or unnecessarily changing the public query, command, notification, and pipeline contracts.

Native AOT performs trimming and cannot safely support unbounded runtime reflection or runtime construction of unknown generic types. The generated path must avoid assembly scanning, `MakeGenericType`, and `Activator.CreateInstance`.

## Architectural direction

Treat generated registration as a second input into the same runtime registry:

```text
Reflection scanner ──┐
                     ├── Registry builder ── FrozenDictionary
Generated code ──────┘
```

The reflection path remains convenient for normal applications. The generated path supplies compile-time-known handler and wrapper registrations for trimmed and Native AOT applications.

Do not create a separate dispatcher implementation for the first AOT release. Registration is the current reflection-heavy area; steady-state dispatch should continue using the same frozen registry and wrappers.

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
Dispatcher.Extensions.DependencyInjection
Dispatcher.SourceGeneration
```

`Dispatcher.SourceGeneration` should use an incremental Roslyn generator. Keeping it separate prevents reflection-only consumers from acquiring Roslyn dependencies and allows generator releases to evolve independently.

The generator should run in every module containing handlers. Generated code is compiled into that module and can therefore reference its internal handler types directly.

Prefer explicit generated module registration, for example:

```csharp
services.AddDispatcher();
services.AddGeneratedOrdersHandlers();
services.AddGeneratedStockHandlers();
```

An alternative is a generated marker implementing a static registration contract:

```csharp
services.AddGeneratedDispatcherHandlers<OrdersModule>();
```

Choose the final shape after prototyping discoverability, naming collisions, and partial-type requirements. Do not rely on experimental call-site interception merely to preserve the reflection method name.

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

Open generic behaviors require special handling because an AOT compiler must see every required closed generic instantiation.

For known request shapes, generated registration should close applicable behaviors explicitly:

```csharp
services.AddScoped<
    IPipelineBehavior<CreateOrderCommand, Guid>,
    ValidationBehavior<CreateOrderCommand, Guid>>();
```

The first generator version should support:

- closed query and command types;
- closed handler implementations;
- resultless commands represented as `Unit` in the pipeline;
- open generic behaviors that can be closed for known requests;
- compile-time validation of generic constraints;
- deterministic behavior ordering matching registration order.

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

Add a dedicated application:

```text
samples/Dispatcher.NativeAotSample
```

It should demonstrate:

- `WebApplication.CreateSlimBuilder`;
- generated module registration;
- internal handlers in separate module assemblies;
- queries and both command shapes;
- multiple notification handlers;
- a FluentValidation command behavior;
- source-generated `System.Text.Json` metadata.

CI should publish a native executable, start it, and exercise its endpoints:

```bash
dotnet publish samples/Dispatcher.NativeAotSample \
  -c Release \
  -r linux-x64 \
  /p:PublishAot=true
```

Success criteria include zero unexpected trimming/AOT warnings and behavior matching the JIT application. Compilation alone is insufficient.

## Package compatibility settings

When the generated path is functional, evaluate enabling the following on relevant library projects:

```xml
<IsTrimmable>true</IsTrimmable>
<IsAotCompatible>true</IsAotCompatible>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
<EnableAotAnalyzer>true</EnableAotAnalyzer>
```

Enable these only after all warnings have been understood and the native sample passes. Reflection entry points may remain annotated as incompatible while the generated path is AOT-safe.

## Delivery sequence

1. Add typed manual handler and wrapper registration. **Complete.**
2. Publish and execute an AOT sample using only manual registration. **Complete.**
3. Add the incremental generator that emits the same registration calls.
4. Add compile-time handler diagnostics.
5. Generate closed registrations for applicable open generic behaviors.
6. Add native publish and endpoint smoke tests to CI.
7. Benchmark reflection startup against generated startup and measure application size.
8. Evaluate generated dispatch or pipeline execution only as a later performance feature.

The first AOT milestone is successful warning-free native publication with correct behavior. Generated dispatch code is not required for that milestone.
