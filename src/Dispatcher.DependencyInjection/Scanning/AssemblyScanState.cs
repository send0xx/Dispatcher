using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Tracks the handler assemblies and route targets scanned into one service collection.
/// </summary>
internal sealed class AssemblyScanState
{
    internal HashSet<Assembly> HandlerAssemblies { get; } = [];

    internal MessageRouteTargets RouteTargets { get; } = new();

    internal static AssemblyScanState? FindScanState(IServiceCollection services)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(AssemblyScanState) &&
                descriptor.ImplementationInstance is AssemblyScanState state)
            {
                return state;
            }
        }

        return null;
    }

    internal static AssemblyScanState CreateScanState(IServiceCollection services)
    {
        var scanState = new AssemblyScanState();
        services.AddSingleton(scanState);
        return scanState;
    }
}