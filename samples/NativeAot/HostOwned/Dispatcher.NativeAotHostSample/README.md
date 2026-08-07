# Dispatcher Native AOT host-registration sample

This .NET 10 Minimal API references two modules containing public messages and internal handlers.
The host owns composition and one generated dispatcher explicitly:

```csharp
builder.Services
    .AddMessageHandlers()
    .AddAuditHandlers()
    .AddDispatcher()
    .AddSingleton<MessageStore>();
```

Each module generates its own handler registrations so its handler implementations remain internal.
The host generates one dispatcher whose routes include both modules. The reflection implementation
is not used.

Publish a native executable for the current platform:

```bash
dotnet publish samples/NativeAot/HostOwned/Dispatcher.NativeAotHostSample -c Release
```
