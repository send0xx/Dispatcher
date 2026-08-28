namespace Dispatcher.DependencyInjection;

internal static class CompatibilityMessages
{
    internal const string DispatcherDynamicCode =
        "The reflection-based Dispatcher requires runtime generic construction. " +
        "Use Dispatcher.SourceGeneration for Native AOT applications.";

    internal const string DispatcherTrimming =
        "The reflection-based Dispatcher is not trimming safe. " +
        "Use Dispatcher.SourceGeneration for trimmed applications.";

    internal const string HandlerDynamicCode =
        "Reflection-based handler discovery requires runtime generic construction. " +
        "Use Dispatcher.SourceGeneration for Native AOT applications.";

    internal const string HandlerTrimming =
        "Reflection-based handler discovery is not trimming safe. " +
        "Use Dispatcher.SourceGeneration for trimmed applications.";

    internal const string WrapperDynamicCode =
        "Creating handler wrappers from service descriptors requires runtime generic construction.";

    internal const string WrapperTrimming =
        "Creating handler wrappers from service descriptors is not trimming safe.";

    internal const string BehaviorDynamicCode =
        "Reflection-based behavior registration is not Native AOT safe. " +
        "Register closed behavior service types directly for Native AOT applications.";

    internal const string BehaviorTrimming =
        "Reflection-based behavior registration is not trimming safe. " +
        "Register closed behavior service types directly for trimmed applications.";
}