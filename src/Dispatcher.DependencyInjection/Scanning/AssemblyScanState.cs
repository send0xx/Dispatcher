using System.Reflection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// What one service collection has already been scanned into, kept as a singleton so that later
/// calls stay idempotent and can route messages their own assemblies did not declare.
/// </summary>
internal sealed class AssemblyScanState
{
    internal HashSet<Assembly> HandlerAssemblies { get; } = [];

    internal MessageRouteTargets RouteTargets { get; } = new();

    internal bool HasOpenNotificationHandlers { get; set; }

    /// <summary>
    /// Route metadata for the scanned messages registry creation still has to reconsider, so that
    /// routing does not depend on the order the registration methods were called in.
    /// </summary>
    /// <param name="handlers">Every handler registration the registry is being created from.</param>
    internal IEnumerable<MessageRegistration> PendingRouteTargets(IEnumerable<HandlerRegistration> handlers) =>
        RouteTargets.PendingRouteTargets(handlers)
            .Select(static messageType => new MessageRegistration(messageType));
}