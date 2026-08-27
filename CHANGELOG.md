# Changelog

This file records user-facing changes to Dispatcher releases.

## 2.0.0

A breaking release that separates handler registrations from route-target metadata and reworks
reflection assembly scanning.

### Changed

- **Breaking:** `HandlerRegistration` no longer derives from `MessageRegistration`. It now declares its
  own `MessageType` property, so a handler registration is no longer usable where a
  `MessageRegistration` is expected.
- **Breaking:** `DispatcherRegistry.Create` was removed and building a registry by hand is no longer
  supported. `AddDispatcher` registers the registry as a singleton that includes the route targets
  discovered by handler scanning; resolve it with `serviceProvider.GetRequiredService<DispatcherRegistry>()`.
- Reflection scanning retains discovered route targets in one internal catalog instead of adding a
  `MessageRegistration` service descriptor for every routable concrete message.

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
