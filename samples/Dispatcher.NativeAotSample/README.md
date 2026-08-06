# Dispatcher Native AOT sample

This .NET 10 Minimal API uses typed module-local handler registration, typed closed pipeline behavior registration, and source-generated JSON metadata. It does not call the reflection-based handler or behavior registration APIs.

Publish a native executable for the current platform:

```bash
dotnet publish samples/Dispatcher.NativeAotSample -c Release
```

Run the published executable and use the same stock and order requests documented by the regular Minimal API sample.
