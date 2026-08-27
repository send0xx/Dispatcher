# Changelog

This file records user-facing changes to Dispatcher releases.

## Unreleased

### Changed

- **Breaking:** `HandlerRegistration` no longer derives from `MessageRegistration`.
- **Breaking:** Registry creation moved from `DispatcherRegistry.Create` to the
  `IServiceProvider.CreateDispatcherRegistry` extension, which includes route targets discovered by handler scanning.
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
