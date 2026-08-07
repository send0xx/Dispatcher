namespace Dispatcher.DependencyInjection;

internal static class CompatibilityMessages
{
    internal const string HandlerDynamicCode =
        "Reflection-based handler discovery requires runtime generic construction. " +
        "Use typed handler registration for Native AOT applications.";

    internal const string HandlerTrimming =
        "Reflection-based handler discovery is not trimming safe. " +
        "Use typed handler registration for trimmed applications.";

    internal const string BehaviorDynamicCode =
        "Reflection-based behavior registration is not Native AOT safe. " +
        "Register closed behavior service types directly for Native AOT applications.";

    internal const string BehaviorTrimming =
        "Reflection-based behavior registration is not trimming safe. " +
        "Register closed behavior service types directly for trimmed applications.";
}