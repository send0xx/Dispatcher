# Dispatcher samples

Choose the sample that matches the registration style you want to learn:

- [`Reflection/Dispatcher.SampleApi`](Reflection/Dispatcher.SampleApi): reflection-based modular application. Orders and Stock own their handler scanning through `AddDispatcherHandlers<TMarker>()`.
- [`NativeAot/HostOwned/Dispatcher.NativeAotHostSample`](NativeAot/HostOwned/Dispatcher.NativeAotHostSample): two generated modules with internal handlers composed into one host-generated dispatcher.

The Native AOT application uses `WebApplication.CreateSlimBuilder` and source-generated JSON metadata.
