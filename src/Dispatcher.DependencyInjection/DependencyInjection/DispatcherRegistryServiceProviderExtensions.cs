using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Provides registry creation methods for Microsoft dependency injection service providers.
/// </summary>
internal static class DispatcherRegistryServiceProviderExtensions
{
    /// <summary>
    /// Creates a registry from the handler and route metadata registered in the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider containing Dispatcher registrations.</param>
    /// <returns>A registry containing the routes known to the service provider.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serviceProvider"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="DuplicateHandlerException">A query or command has multiple handlers.</exception>
    /// <exception cref="AmbiguousHandlerException">
    /// A concrete message matches multiple unrelated handled message types.
    /// </exception>
    [RequiresDynamicCode(CompatibilityMessages.DispatcherDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.DispatcherTrimming)]
    internal static DispatcherRegistry CreateDispatcherRegistry(this IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var handlers = serviceProvider.GetServices<HandlerRegistration>().ToArray();
        var routeTargets = serviceProvider.GetServices<MessageRegistration>()
            .Select(static registration => registration.MessageType);
        if (serviceProvider.GetService<AssemblyScanState>() is { } scanState)
        {
            routeTargets = routeTargets.Concat(scanState.RouteTargets.GetRouteTargets(handlers));
        }

        return DispatcherRegistryFactory.Create(
            handlers,
            routeTargets,
            serviceProvider.GetService<DispatcherTelemetry>());
    }
}