# Dispatcher Native AOT host-registration sample

This .NET 10 Minimal API references a shared contracts assembly and two modules containing internal
handlers. `GetAuditCountQuery` remains beside its handler to demonstrate that colocated messages are
supported alongside shared contracts. The host owns composition and one generated dispatcher explicitly:

```csharp
builder.Services
    .AddDispatcher()
    .AddMessageHandlers()
    .AddAuditHandlers()
    .AddPipelineBehavior(typeof(LoggingBehavior<,>))
    .AddSingleton<MessageStore>();
```

Each module generates its own handler registrations so its handler implementations remain internal.
The host generates one dispatcher whose routes include both modules and their shared contracts. The
reflection implementation is not used. `ListMessagesQuery` routes to the base `MessagesQuery` handler.
After adding a message, its command handler publishes `MessageAdded`, which routes to the base
`MessageEvent` handlers in both the Messages and Audit modules. The generator also closes
`LoggingBehavior<,>` over every known query and command without runtime generic construction.

Publish a native executable for the current platform:

```bash
dotnet publish samples/NativeAot/Dispatcher.NativeAotHostSample -c Release
```
