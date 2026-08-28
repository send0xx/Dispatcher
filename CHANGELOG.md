# Changelog

This file records user-facing changes to Dispatcher releases.

## 2.0.0

A breaking release that removes public dispatch metadata and reworks reflection assembly scanning.

### Changed

- **Breaking:** `HandlerNotFoundException` moved from `Send0xx.Dispatcher.Abstractions` to
  `Send0xx.Dispatcher`. Its namespace and observable dispatch behavior are unchanged.
- **Breaking:** `HandlerRegistration`, its query, command, and notification subclasses, and
  `MessageRegistration` were removed. Handler registration methods now add only executable handler
  service descriptors to Microsoft DI.
- **Breaking:** Use `AddDispatcherMessage<TMessage>()` or `AddDispatcherMessage(Type)` instead of
  registering `MessageRegistration` when the reflection implementation needs an explicit polymorphic
  route target.
- **Breaking:** `DispatcherRegistry.Create` and `CreateDispatcherRegistry` were removed, and building a
  registry by hand is no longer supported. `AddDispatcher` registers the registry as a singleton that
  includes the route targets discovered by handler scanning; resolve it with
  `serviceProvider.GetRequiredService<DispatcherRegistry>()`.
- Reflection scanning retains discovered route targets in one internal catalog instead of adding a
  `MessageRegistration` service descriptor for every routable concrete message.
- The reflection registry derives handler information from the final handler service descriptors when
  its singleton is created. The source-generated implementation also uses ordinary service descriptors
  to select registered open generic notification handlers.

### Fixed

- Assembly scanning now validates handler and route-target assemblies before changing the service
  collection, so a type-load failure cannot leave partially registered handlers or stale scan state.

## 1.0.0

The first stable Dispatcher release.

### Added

- Query, result-returning command, resultless command, and notification dispatch contracts.
- Reflection-based Microsoft dependency-injection integration with explicit assembly scanning, internal handler support, scoped dispatchers, configurable handler lifetimes, and idempotent registrations.
- Source-generated handler registration and dispatch for trimming and Native AOT applications, including internal handlers composed across assemblies.
- A unified pipeline behavior contract with ordered execution, short-circuiting, cancellation-token replacement, and typed registration helpers.
- Exact and precomputed polymorphic handler routes for base classes and interfaces.
- Optional OpenTelemetry activities and operation-duration metrics.
- Generator diagnostics DSPG001 through DSPG010 for invalid registrations and unsupported handler or route shapes.
- Packages targeting .NET 8 and .NET 10.
