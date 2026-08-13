# Dispatcher samples

Choose the sample that matches the registration style you want to learn:

- [`DependencyInjection/Dispatcher.SampleApi`](DependencyInjection/Dispatcher.SampleApi): reflection-based modular application. Shared contracts are separate from the Orders and Stock handler modules, which own their scanning through `AddDispatcherHandlers<TMarker>()`.
- [`NativeAot/Dispatcher.NativeAotHostSample`](NativeAot/Dispatcher.NativeAotHostSample): shared contracts and two generated modules with internal handlers composed into one host-generated dispatcher.

Both samples keep some queries beside their handlers while placing other messages in shared contracts.
They also include polymorphic routes from concrete contract messages to base-type handlers. The Native
AOT application uses `WebApplication.CreateSlimBuilder` and source-generated JSON metadata.
