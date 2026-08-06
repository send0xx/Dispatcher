# Dispatcher Native AOT host-registration sample

This .NET 10 Minimal API references a separate assembly containing public messages, internal handlers, and generated registration metadata. The host owns composition explicitly:

```csharp
builder.Services
    .AddDispatcher()
    .AddGeneratedMessageHandlers()
    .AddSingleton<MessageStore>();
```

Publish a native executable for the current platform:

```bash
dotnet publish samples/NativeAot/HostOwned/Dispatcher.NativeAotHostSample -c Release
```
