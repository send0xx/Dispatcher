# Dispatcher v1 implementation plan

> This is the historical plan used to build version 1.0.0. The implemented public API and repository documentation are authoritative; see [AOT.md](AOT.md) for future Native AOT work.

## 1. Goal and initial scope

Build a small, dependency-injection-oriented CQRS dispatcher with:

- separate query, command, and notification contracts;
- exactly one handler for each query or command;
- zero or more handlers for each notification;
- reflection-based handler discovery at application startup;
- immutable `FrozenDictionary` dispatch registries after registration;
- ordered query and command pipeline behaviors for cross-cutting concerns;
- support for internal handler implementation types;
- libraries targeting both .NET 8 and .NET 10, with the sample application targeting .NET 10.

The first version will deliberately exclude source generation, NativeAOT/trimming guarantees, polymorphic dispatch, notification behaviors, streaming requests, and configurable notification strategies. These can be added later without expanding the initial API unnecessarily.

## 2. Proposed solution structure

```text
Dispatcher.slnx
Directory.Build.props
Directory.Packages.props
README.md
LICENSE
src/
  Dispatcher.Abstractions/
    Dispatcher.Abstractions.csproj
  Dispatcher/
    Dispatcher.csproj
  Dispatcher.Extensions.Microsoft.DependencyInjection/
    Dispatcher.Extensions.Microsoft.DependencyInjection.csproj
tests/
  Dispatcher.Tests/
    Dispatcher.Tests.csproj
samples/
  Dispatcher.SampleApi/
    Dispatcher.SampleApi.csproj
  Dispatcher.SampleApi.Modules.Orders/
    Dispatcher.SampleApi.Modules.Orders.csproj
  Dispatcher.SampleApi.Modules.Stock/
    Dispatcher.SampleApi.Modules.Stock.csproj
```

Package IDs:

- `Dispatcher.Abstractions`: public messages, handlers, behaviors, and dispatcher contracts, with no DI dependency;
- `Dispatcher`: runtime dispatcher, typed wrappers, registry descriptors, exceptions, and `FrozenDictionary` registries, referencing only `Dispatcher.Abstractions` and BCL APIs such as `IServiceProvider`;
- `Dispatcher.Extensions.Microsoft.DependencyInjection`: Microsoft DI integration, reflection scanning, `IServiceCollection` extensions, lifetimes, and service descriptors, referencing both other packages and `Microsoft.Extensions.DependencyInjection.Abstractions`.

All three package projects will multi-target `net8.0;net10.0`. The tests will multi-target both frameworks where installed; the sample projects will prefer and target `net10.0`.

Installing `Dispatcher.Extensions.Microsoft.DependencyInjection` will bring `Dispatcher` and `Dispatcher.Abstractions` transitively. Applications using another container can reference the first two packages and provide their own composition adapter without depending on Microsoft DI.

## 3. Public abstractions

Define small marker and handler interfaces in `Dispatcher.Abstractions`:

```csharp
public interface IRequest;
public interface IQuery<out TResponse> : IRequest;
public interface ICommand<out TResponse> : IRequest;
public interface ICommand : ICommand<Unit>;
public interface INotification;
public readonly record struct Unit;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand;

public interface INotificationHandler<in TNotification>
    where TNotification : INotification;
```

Handler methods will accept a `CancellationToken`. Query and result-bearing command handlers will return `ValueTask<TResponse>`; resultless command and notification handlers will return `ValueTask`. `Unit` gives resultless commands the same strongly typed behavior pipeline, but their handlers and dispatcher overload do not require application code to return it.

Define matching behavior contracts and explicit `next` delegates:

```csharp
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>(
    CancellationToken cancellationToken);

public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest;
```

Each behavior's `HandleAsync` method will receive the message, its matching `next` delegate, and a `CancellationToken`. Passing the token explicitly through `next(cancellationToken)` avoids requiring the pipeline to capture it and allows a behavior to replace or link the token deliberately. The first registered behavior will be the outermost behavior and therefore execute first before the handler and last after it.

Expose role-specific dispatch contracts plus a convenience aggregate:

- `IQueryDispatcher.QueryAsync<TResponse>(IQuery<TResponse>, CancellationToken)`;
- `ICommandDispatcher.ExecuteAsync<TResponse>(ICommand<TResponse>, CancellationToken)`;
- `ICommandDispatcher.ExecuteAsync(ICommand, CancellationToken)`;
- `INotificationPublisher.PublishAsync<TNotification>(TNotification, CancellationToken)`;
- `IDispatcher`, combining all three contracts.

The implementation will validate null messages and report missing or ambiguous request handlers with dedicated, descriptive exceptions. Publishing a notification with no handlers will be a successful no-op.

## 4. Runtime and DI assembly boundaries

The `Dispatcher` runtime package will own dispatch lookup and invocation but will not reference `Microsoft.Extensions.DependencyInjection`. It will resolve closed handler and behavior service types through standard `IServiceProvider.GetService(Type)` calls; collection resolution will request the corresponding closed `IEnumerable<T>` service type. Registry construction inputs will be exposed through a deliberately narrow runtime API so the DI adapter can construct the frozen registry without making wrapper implementation details public.

The `Dispatcher.Extensions.Microsoft.DependencyInjection` package will own every Microsoft-specific concept: `IServiceCollection`, `ServiceDescriptor`, `ServiceLifetime`, `TryAdd` behavior, assembly scanning, and the `AddDispatcher`, `AddDispatcherHandlers`, and `AddPipelineBehavior` extension methods. Namespace naming will follow the package boundary so consumers explicitly opt into the DI integration with a `using` directive.

## 5. Discovery and DI registration

Separate dispatcher infrastructure registration from handler registration:

```csharp
// Web application composition root
services.AddDispatcher();
services.AddOrdersModule();
services.AddStockModule();

// Inside the Orders module's public registration method
public static IServiceCollection AddOrdersModule(this IServiceCollection services)
{
    services.AddOrdersPersistence();
    return services.AddDispatcherHandlers(typeof(OrdersModule).Assembly);
}
```

`AddDispatcher()` will register only the dispatcher, role-specific interfaces, registry factory, and default infrastructure. It will not scan the entry assembly or implicitly register any handlers. Repeated calls will be idempotent.

`AddDispatcherHandlers(...)` will register handlers belonging to one module. Provide `Assembly`, `params Assembly[]`, and generic marker-type overloads so module code does not have to expose its assembly details to the host, for example `AddDispatcherHandlers<TModuleMarker>()`. Repeated registration of the same assembly will be de-duplicated. A parameterless reflection overload will not infer the calling assembly because that becomes unreliable when methods are inlined; the generic marker overload provides the concise, deterministic equivalent.

The initial implementation will use BCL reflection rather than adding Scrutor: scan concrete, non-open-generic types and inspect their implemented closed handler interfaces. Internal classes are included because discovery uses all types from the module assembly rather than public exported types. Calling this from each module's own public DI extension makes module ownership explicit and avoids a composition-root scan across unrelated assemblies.

This split is also the migration seam for future AOT support. A source generator can later emit a module-local `AddDispatcherHandlers` implementation containing direct registrations for internal handlers, while `AddDispatcher()` and the dispatch API remain unchanged. The reflection overloads will be documented as not trimming/AOT safe in v1.

Behaviors will be registered explicitly so their order is visible at the composition root. Provide generic convenience methods for closed behaviors and `Type`-based overloads for open-generic behaviors, for example:

```csharp
services
    .AddDispatcher()
    .AddDispatcherHandlers<ApplicationMarker>()
    .AddPipelineBehavior(typeof(LoggingBehavior<,>));
```

`AddPipelineBehavior` will register the single `IPipelineBehavior<TRequest, TResponse>` contract used by queries and every command. Resultless commands flow through it with `Unit` as `TResponse`. It will have overloads such as `AddPipelineBehavior<TBehavior>()` and `AddPipelineBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped)`.

The registration method will validate that a supplied implementation has at least one supported behavior interface shape and reject unrelated, abstract, or improperly open types with a descriptive startup error. Behavior lifetime will be scoped by default and configurable per registration. Open-generic behaviors may use normal generic constraints to select compatible messages; the Microsoft DI container will resolve only behaviors whose closed construction is valid.

Registration behavior:

1. `AddDispatcher()` uses `TryAdd`-style registrations so infrastructure is added once.
2. Each `AddDispatcherHandlers(...)` call de-duplicates supplied assemblies and discovered `(service type, implementation type)` pairs across prior calls.
3. Register every discovered closed handler interface with its concrete implementation as scoped.
4. Collect lightweight registry descriptors from all module registration calls without freezing a partial registry prematurely.
5. Reject duplicate query/command handlers across modules with an error naming the message and both handler types. Validation occurs no later than service-provider validation/first registry construction.
6. Allow multiple notification handlers and preserve module-call then deterministic within-module registration order.
7. Preserve behavior registration order and permit multiple behaviors for the same message shape.
8. When the singleton registry is first constructed, consume all collected descriptors, create wrapper instances once, build the complete dictionaries, and call `ToFrozenDictionary()`.
9. Register one scoped dispatcher implementation and map `IDispatcher` plus each role-specific interface to that same scoped instance.

`AddDispatcherHandlers` overloads will allow changing handler lifetime (`Scoped` by default). Assembly scanning and registry construction remain startup work; dispatch itself will not use reflection.

## 6. FrozenDictionary dispatch implementation

Use two immutable registries:

- a request registry keyed by the concrete query/command runtime `Type` and containing a non-generic request-wrapper base;
- a notification registry keyed by notification `Type` and containing a non-generic notification-wrapper base.

Query and command wrapper instances will be closed once at startup with `MakeGenericType`/`Activator.CreateInstance`. Separate internal wrappers will cover queries, result-bearing commands, and resultless commands while sharing a non-generic registry value base. At dispatch time a wrapper performs a strongly typed cast and resolves the closed handler and applicable behavior collection from the current scope's `IServiceProvider`.

Each wrapper will build the invocation chain around the handler in reverse enumeration order, making the first registered behavior outermost. The completed chain is then invoked with the dispatch cancellation token. Reflection and generic type construction remain startup-only; behavior composition uses typed wrapper code. The initial implementation may allocate delegates while composing a pipeline per dispatch; performance optimization or cached compiled chains can follow benchmarks without changing the public contracts.

Notification wrappers will resolve all `INotificationHandler<TNotification>` instances and await them sequentially in registration order. Sequential execution gives deterministic behavior and stops on the first exception. Cancellation tokens flow unchanged to every handler.

Exact runtime type matching will be documented. Base-type/interface polymorphic routing is outside v1 because it complicates duplicate resolution and notification fan-out semantics.

## 7. Beginner-friendly .NET 10 Minimal API sample

Create one small ASP.NET Core Minimal API as the composition root and two class-library modules. Keep the example deliberately compact: endpoint mappings, commands, queries, validators, handlers, and in-memory stores should be easy to navigate without infrastructure unrelated to demonstrating Dispatcher.

Suggested endpoints:

```text
POST /orders
GET  /orders
GET  /orders/{id}
GET  /stock/{productId}
PUT  /stock/{productId}
```

Modules:

- **Orders module**
  - create an order command with product ID and positive quantity validation;
  - get/list orders queries;
  - publish `OrderCreated` after creation;
  - internal FluentValidation validator and internal command/query handlers;
  - a simple thread-safe in-memory order repository for a self-contained sample.
- **Stock module**
  - query current stock;
  - set/adjust stock command with product ID and non-negative quantity validation;
  - internal notification handler for `OrderCreated` that updates/reserves stock;
  - internal FluentValidation validator, internal query/command handlers, and an in-memory stock repository.

Each module will expose only a public marker/module registration entry point; message contracts needed across module boundaries will be public, while handler classes remain internal. Each module registration method will call `AddDispatcherHandlers` for its own assembly. The web host will call `AddDispatcher()` once, add both modules independently, map minimal API endpoints, and inject the narrow dispatcher interface needed by each endpoint.

The sample flow will demonstrate both point-to-point dispatch and cross-module notification fan-out: creating an order updates Orders state and publishes an event observed by Stock without the web host knowing the handler type.

Add FluentValidation packages to the sample only, not to any Dispatcher package. Each module will register its own internal `IValidator<TCommand>` implementations alongside its internal dispatcher handlers.

Add one open-generic command validation behavior at the web composition root, constrained as `IPipelineBehavior<TCommand, TResponse>` where `TCommand : ICommand<TResponse>`. It will resolve all `IValidator<TCommand>` instances, validate before calling `next`, aggregate failures, and throw FluentValidation's `ValidationException` without executing the handler when validation fails. The same behavior covers result-bearing and resultless commands and is registered through `AddPipelineBehavior(...)`.

Add a small ASP.NET Core exception handler that translates `ValidationException` into an RFC 7807 validation problem response with HTTP 400. Keep HTTP DTO-to-command mapping directly in the endpoint definitions so beginners can see the full request path without extra mapping libraries.

The sample README will walk through one request end to end: Minimal API endpoint -> command dispatcher -> FluentValidation behavior -> internal command handler -> notification publisher -> internal Stock notification handler. Logging and other production concerns will be mentioned as possible behaviors but omitted from the example to keep its teaching purpose focused.

## 8. Tests

Add focused xUnit tests covering:

- query dispatch and typed response;
- result-bearing and resultless command dispatch;
- cancellation-token propagation;
- query, result-bearing command, and resultless command behavior execution;
- multiple behavior ordering before and after the handler;
- behavior short-circuiting without invoking the handler;
- behavior and handler exception propagation;
- constrained open-generic and closed behavior applicability;
- scoped dependencies inside behaviors;
- sample command validation preventing handler execution and producing HTTP 400 validation details;
- valid sample commands reaching their handlers through the FluentValidation behavior;
- multiple notification handlers executing in deterministic order;
- notification with no handlers as a no-op;
- missing query/command handler errors;
- duplicate query/command handler rejection at registration;
- dispatcher infrastructure registration without implicit assembly scanning;
- the core `Dispatcher` project having no Microsoft.Extensions dependency;
- the DI extension package correctly composing the runtime package;
- handlers added before or after `AddDispatcher()` producing the same complete registry;
- multiple module-level `AddDispatcherHandlers` calls contributing to one frozen registry;
- repeated infrastructure and module registration being idempotent;
- internal handler discovery from a scanned assembly;
- exact runtime-type lookup behavior;
- scoped handler and dispatcher lifetimes, including dependency resolution from the active scope;
- all dispatcher interfaces resolving to the same scoped implementation;
- registry types/properties being backed by `FrozenDictionary`;
- null argument validation and handler exception propagation.

Run `dotnet restore`, `dotnet build`, and `dotnet test` for both target frameworks. Since this machine currently has only the .NET 10 SDK/runtime installed, .NET 8 execution may require installing the .NET 8 runtime; compilation of `net8.0` should still be validated through the .NET 10 SDK reference packs when available.

## 9. Packaging and documentation

- Add NuGet metadata: package ID, description, authors, repository URL placeholder, symbols package, Source Link, README, license, and deterministic builds.
- Keep versioning centralized and start with a prerelease version such as `0.1.0-preview.1` until the API is reviewed.
- Enable nullable reference types, implicit usings, XML documentation generation, and package validation.
- Document installation, registration, all message shapes, internal-handler scanning, lifetimes, notification ordering/error semantics, and current non-AOT limitation.
- Pack all three libraries locally and add smoke checks that consume the generated packages rather than project references: one with Microsoft DI through `Dispatcher.Extensions.Microsoft.DependencyInjection`, and one minimal custom `IServiceProvider` check against `Dispatcher` to enforce the container-neutral boundary.

## 10. Implementation sequence

1. Scaffold solution, shared build/package configuration, three package projects, and test matrix.
2. Implement and document the abstractions.
3. Implement the container-neutral runtime: wrapper types, registry descriptors, frozen-registry construction, dispatch, and behavior pipeline composition.
4. Implement the separate Microsoft DI adapter: infrastructure/handler registration, reflection discovery, cross-module validation, lifetimes, and deferred registry construction.
5. Add unit/integration tests and resolve behavior/API issues they expose.
6. Build the compact Orders and Stock modules, FluentValidation command behaviors, validation exception mapping, and .NET 10 Minimal API endpoints.
7. Add package metadata, README usage examples, local pack/consumer smoke tests, and final build/test/pack verification.

## 11. Decisions to confirm during plan review

The plan currently assumes:

- `ValueTask`-based handlers and dispatcher methods;
- explicit query and command contracts instead of one generic request abstraction;
- resultless `ICommand` derives from `ICommand<Unit>` so every command has a pipeline response type;
- command dispatch methods are named `ExecuteAsync` to express command execution rather than generic message sending;
- `AddDispatcher()` registers infrastructure only and never scans for handlers implicitly;
- every module owns a separate `AddDispatcherHandlers(...)` call for its internal handlers;
- the registry is frozen only after contributions from all registered modules can be collected;
- scoped handlers and dispatcher by default, configurable as one common handler lifetime;
- one `IPipelineBehavior<TRequest, TResponse>` contract for all queries and commands;
- one `AddPipelineBehavior` registration method name with generic and `Type` overloads;
- explicitly registered behaviors with first-registered/outermost ordering across repeated `AddPipelineBehavior` calls;
- scoped behavior lifetime by default, configurable per behavior registration;
- sequential notification execution, registration order, and stop-on-first-error behavior;
- exact concrete-type dispatch only;
- query and command behaviors in v1, but no notification behavior pipeline;
- FluentValidation integration belongs to the sample application and is not a dependency of the reusable packages;
- contracts live in `Dispatcher.Abstractions`, the container-neutral runtime lives in `Dispatcher`, and Microsoft DI registration lives exclusively in `Dispatcher.Extensions.Microsoft.DependencyInjection`;
- an MIT license and placeholder repository/author metadata until real values are supplied.

These are intentionally called out because changing them later could affect the public API or observable behavior.

## References reviewed

- [SwitchMediator](https://github.com/zachsaw/SwitchMediator): separation of contracts/DI concerns, typed wrappers, and frozen lookup ideas; source-generation/AOT features are deferred.
- [CQRS Without MediatR](https://github.com/codewithmukesh/dotnet-webapi-zero-to-hero-course/tree/main/modules/03-advanced-api-patterns/cqrs-without-mediatr): startup reflection, wrapper construction, scoped DI resolution, `FrozenDictionary` registries, and sequential notifications.
- [Mediator](https://github.com/martinothamar/Mediator/tree/main/src/Mediator): small sender/publisher/mediator contract separation and handler/message organization.
