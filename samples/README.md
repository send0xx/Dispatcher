# Dispatcher samples

Choose the sample that matches the registration style you want to learn:

- [`Reflection/Dispatcher.SampleApi`](Reflection/Dispatcher.SampleApi): reflection-based modular application. Orders and Stock own their handler scanning through `AddDispatcherHandlers<TMarker>()`.
- [`NativeAot/ModuleOwned/Dispatcher.NativeAotSample`](NativeAot/ModuleOwned/Dispatcher.NativeAotSample): generated registration hidden behind the referenced Counter module's `AddCounterModule()` method.
- [`NativeAot/HostOwned/Dispatcher.NativeAotHostSample`](NativeAot/HostOwned/Dispatcher.NativeAotHostSample): generated registration from a referenced handler assembly called explicitly by the main host.

Both Native AOT applications use `WebApplication.CreateSlimBuilder` and source-generated JSON metadata.
