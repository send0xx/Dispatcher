using System.Reflection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Tracks the handler assemblies and route targets scanned into one service collection.
/// </summary>
internal sealed class AssemblyScanState
{
    internal HashSet<Assembly> HandlerAssemblies { get; } = [];

    internal MessageRouteTargets RouteTargets { get; } = new();
}