---
uid: guide.opentelemetry
title: OpenTelemetry
description: Enable Dispatcher tracing activities and the operation-duration histogram.
---

# OpenTelemetry

Dispatcher can emit tracing activities and an operation-duration histogram through the built-in .NET
diagnostics APIs. Both signals are **disabled by default**, and disabled telemetry adds no work to the
dispatch path.

Dispatcher itself does not depend on the OpenTelemetry SDK or an exporter.

## Enabling the signals

Enable either signal through `DispatcherOptions.Telemetry`:

```csharp
builder.Services.AddDispatcher(options =>
{
    options.Telemetry.EnableTracing = true;
    options.Telemetry.EnableMetrics = true;
});
```

## Registering with OpenTelemetry

The default activity source and meter name is `Dispatcher`. Register those names with the
application's OpenTelemetry providers:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Dispatcher"))
    .WithMetrics(metrics => metrics.AddMeter("Dispatcher"));
```

Use `ActivitySourceName` and `MeterName` to choose application-specific names.

> [!IMPORTANT]
> If you change `ActivitySourceName` or `MeterName`, pass the same values to `AddSource` and
> `AddMeter`. A mismatch produces no telemetry and no error.

## What is measured

Telemetry surrounds the **complete routed operation**: outside user pipeline behaviors, and around
all sequential notification handlers.

| Signal | Emitted |
| --- | --- |
| Histogram | `dispatcher.operation.duration`, in seconds |
| Attributes | `dispatcher.operation.name`, `dispatcher.message.type`, `dispatcher.message.kind` |

The attributes are included on both traces and metrics.

## What is not measured

Missing query or command handlers and notifications with no handlers do **not** emit telemetry.

Failures, including cancellation, set `error.type`, mark the activity as an error, and add a standard
exception event.
