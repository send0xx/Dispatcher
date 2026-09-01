# Changelog

This file records user-facing changes to Dispatcher releases.

## Unreleased

### Changed

- **Breaking:** The `AddNotificationHandler(Type)` overload was removed. Reflection assembly scanning discovers open
  generic notification handlers, while source generation emits their closed registrations. The typed
  `AddNotificationHandler<TNotification, THandler>()` overload remains in `Dispatcher` for explicit closed-handler
  registration.
- Source-generated open notification handlers now use explicit closed service descriptors for every compatible known
  notification type. This removes Microsoft DI's runtime open-generic closure from the generated path and supports
  value-type notifications under Native AOT while preserving module lifetimes and registration order.
- Source generation was rewritten around separate discovery, compilation analysis, route resolution, and emission
  stages. The generated dispatcher and registration code is more consistently structured and formatted while preserving
  handler routing, notifications, pipelines, telemetry, diagnostics, namespaces, and generated API names.
- Benchmarks were reorganized into focused execution, telemetry, and scale suites. Reflection and source-generated
  dispatch now use the same workloads, while deterministic scale fixtures measure handler scanning, provider startup,
  source generation, incremental changes, assembly emission, pipeline depth, and notification fan-out.

## 2.0.0

A breaking release that removes public dispatch metadata and reworks reflection assembly scanning.

### Added

- `AddDispatcherMessage<TMessage>()` and `AddDispatcherMessage(Type)` register a concrete request or notification type
  as an explicit polymorphic route target for the reflection implementation.
- `Unit` declares the `<`, `<=`, `>`, and `>=` operators. It already implemented `IComparable<Unit>`, and every `Unit`
  value compares equal to every other.

### Changed

- **Breaking:** `HandlerNotFoundException` moved from `Send0xx.Dispatcher.Abstractions` to
  `Send0xx.Dispatcher`. Its namespace and observable dispatch behavior are unchanged.
- **Breaking:** `HandlerRegistration`, its query, command, and notification subclasses, and
  `MessageRegistration` were removed. Handler registration methods now add only executable handler service descriptors
  to Microsoft DI.
- **Breaking:** Use `AddDispatcherMessage<TMessage>()` or `AddDispatcherMessage(Type)` instead of registering
  `MessageRegistration` when the reflection implementation needs an explicit polymorphic route target.
- **Breaking:** `DispatcherRegistry.Create` was removed, and building a registry by hand is no longer supported.
  `AddDispatcher` builds the registry itself, including the route targets discovered by handler scanning.
- **Breaking:** The reflection implementation `Dispatcher` and its `DispatcherRegistry` are now internal. Once
  `DispatcherRegistry.Create` was removed neither type exposed a member a consumer could call, and the registry could no
  longer be constructed by hand. Resolve `IDispatcher`,
  `IQueryDispatcher`, `ICommandDispatcher`, or `INotificationDispatcher` instead.
- **Breaking:** `DispatcherTelemetry` is now internal, matching the telemetry type the source generator already emits.
  Its only public members were a constructor and `Dispose`. Configure telemetry through `DispatcherOptions.Telemetry`
  and `DispatcherTelemetryOptions`, and subscribe to the meter and activity source by name.
- Reflection scanning retains discovered route targets in one internal catalog instead of adding a
  `MessageRegistration` service descriptor for every routable concrete message.
- The reflection registry derives handler information from the final handler service descriptors when its singleton is
  created. The source-generated implementation also uses ordinary service descriptors to select registered open generic
  notification handlers.
- Because the reflection registry reads handler service descriptors, a handler registered directly with Microsoft
  dependency injection now routes. Previously only the typed registration methods and assembly scanning produced a
  route, so dispatching a message whose handler was added through
  `IServiceCollection` alone threw `HandlerNotFoundException`. Registering through the Dispatcher methods remains the
  supported path, because they also validate the handler shape.
- A query or command with more than one registered handler now throws `DuplicateHandlerException`
  when the registry singleton is created, including when one of the registrations is a factory delegate. Previously a
  factory registration carried no metadata, so it silently replaced the handler that the typed registration method had
  routed.
- `AssemblyScanException.LoaderExceptions` returns the same collection instance on every call. It previously allocated a
  new array on each property read.

### Fixed

- Assembly scanning now validates handler and route-target assemblies before changing the service collection, so a
  type-load failure cannot leave partially registered handlers or stale scan state.
- An open generic notification handler mapped to the handler interface with Microsoft dependency injection, such as
  `AddScoped(typeof(INotificationHandler<>), typeof(AuditHandler<>))`, now routes. Creating the registry previously
  threw `ArgumentException` because the reflection implementation read the descriptor as a closed handler whose
  notification type was still a type parameter. Microsoft DI serves such a handler through
  `IEnumerable<INotificationHandler<TNotification>>`, so it runs as part of the selected closed route, and the registry
  now also routes a concrete notification whose only handler is the mapping. A handler that is both mapped and
  registered under its own type, by assembly scanning or `AddNotificationHandler`, runs once rather than twice per
  publish.

## 1.0.0

The first stable Dispatcher release.

### Added

- Query, result-returning command, resultless command, and notification dispatch contracts.
- Reflection-based Microsoft dependency-injection integration with explicit assembly scanning, internal handler support,
  scoped dispatchers, configurable handler lifetimes, and idempotent registrations.
- Source-generated handler registration and dispatch for trimming and Native AOT applications, including internal
  handlers composed across assemblies.
- A unified pipeline behavior contract with ordered execution, short-circuiting, cancellation-token replacement, and
  typed registration helpers.
- Exact and precomputed polymorphic handler routes for base classes and interfaces.
- Optional OpenTelemetry activities and operation-duration metrics.
- Generator diagnostics DSPG001 through DSPG010 for invalid registrations and unsupported handler or route shapes.
- Packages targeting .NET 8 and .NET 10.
