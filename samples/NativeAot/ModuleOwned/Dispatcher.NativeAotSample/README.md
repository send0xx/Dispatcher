# Dispatcher Native AOT sample

This .NET 10 Minimal API references a Counter module containing messages, internal handlers, validation, state, and generated registration metadata. The module owns its composition entry point:

```csharp
builder.Services
    .AddDispatcher()
    .AddCounterModule();
```

`AddCounterModule` registers module services and calls its generated handler method internally. The host also demonstrates typed closed FluentValidation behaviors and source-generated JSON metadata. It does not call reflection-based registration APIs.

`CounterChanged` has two internal notification handlers. One records the last published value and the other counts observed changes, demonstrating generated notification fan-out.

Publish a native executable for the current platform:

```bash
dotnet publish samples/NativeAot/ModuleOwned/Dispatcher.NativeAotSample -c Release
```

The API exposes:

- `POST /counter/increment` with `{ "amount": 3 }`;
- `GET /counter`;
- `DELETE /counter`.
