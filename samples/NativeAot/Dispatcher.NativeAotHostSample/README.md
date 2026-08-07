# Dispatcher Native AOT host-registration sample

This .NET 10 Minimal API references two modules containing public messages and internal handlers.
The host owns composition and one generated dispatcher explicitly:

```csharp
builder.Services
    .AddDispatcher()
    .AddMessageHandlers()
    .AddAuditHandlers()
    .AddPipelineBehavior(typeof(LoggingBehavior<,>))
    .AddSingleton<MessageStore>();
```

Each module generates its own handler registrations so its handler implementations remain internal.
The host generates one dispatcher whose routes include both modules. The reflection implementation
is not used. The generator closes `LoggingBehavior<,>` over every known query and command without
runtime generic construction. After adding a message, its command
handler publishes `MessageAdded`; internal notification handlers in both the Messages and Audit
modules receive it.

Publish a native executable for the current platform:

```bash
dotnet publish samples/NativeAot/Dispatcher.NativeAotHostSample -c Release
```
