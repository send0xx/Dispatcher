using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Tracks the handler assemblies and route targets scanned into one service collection.
/// </summary>
/// <remarks>
/// The state travels with the service collection as a singleton descriptor, so scans and route
/// target registrations spread over several calls share it.
/// </remarks>
internal sealed class AssemblyScanState
{
    internal HashSet<Assembly> HandlerAssemblies { get; } = [];

    internal MessageRouteTargets RouteTargets { get; } = new();

    internal static AssemblyScanState? Find(IServiceCollection services)
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

    internal static AssemblyScanState GetOrCreate(IServiceCollection services)
    {
        if (Find(services) is { } state)
        {
            return state;
        }

        var scanState = new AssemblyScanState();
        services.AddSingleton(scanState);
        return scanState;
    }
}