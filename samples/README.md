# Dispatcher samples

Choose the sample that matches the registration style you want to learn:

- [`DependencyInjection/Dispatcher.SampleApi`](DependencyInjection/Dispatcher.SampleApi): reflection-based modular application. Orders and Stock own their handler scanning through `AddDispatcherHandlers<TMarker>()`.
- [`NativeAot/Dispatcher.NativeAotHostSample`](NativeAot/Dispatcher.NativeAotHostSample): two generated modules with internal handlers composed into one host-generated dispatcher.

The Native AOT application uses `WebApplication.CreateSlimBuilder` and source-generated JSON metadata.
