# Changelog

This file records user-facing changes to Dispatcher releases.

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

### Changed since 1.0.0-rc.2

- Split the source generator into `Dispatcher.SourceGeneration.Analyzers.dll`, while keeping it bundled in `Send0xx.Dispatcher.SourceGeneration` for normal package consumers.
- Made `Send0xx.Dispatcher.SourceGeneration` a .NET 8 and .NET 10 package facade that references `Send0xx.Dispatcher` and ships matching framework assets.
