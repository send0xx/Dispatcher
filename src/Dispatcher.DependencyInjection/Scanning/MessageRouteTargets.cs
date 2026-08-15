using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Tracks the concrete message types scanning has seen but has not been able to route yet, and
/// registers each one as <see cref="MessageRegistration"/> metadata once a handler makes it routable.
/// </summary>
/// <remarks>
/// A message declared by one module often gets its handler from a module registered later, so a type
/// that is unroutable during one scan can become routable during the next. Types therefore stay
/// pending until that happens, and are dropped once routed, so no scan reconsiders a message it has
/// already resolved. Routability depends only on the handled message types and on whether any open
/// generic notification handler is registered, so when neither of those changed a scan only has to
/// consider the message types it just added.
/// </remarks>
internal sealed class MessageRouteTargets
{
    private readonly HashSet<Assembly> _scannedAssemblies = [];
    private readonly List<Type> _pending = [];
    private HashSet<Type>? _lastHandledMessageTypes;

    internal bool NeedsScan(Assembly assembly) => !_scannedAssemblies.Contains(assembly);

    /// <summary>
    /// Marks the current end of the pending list, so that <see cref="Register"/> can tell the message
    /// types this scan adds from the ones earlier scans left unroutable.
    /// </summary>
    internal int Mark() => _pending.Count;

    internal void Add(Assembly assembly, IEnumerable<Type> types)
    {
        if (!_scannedAssemblies.Add(assembly))
        {
            return;
        }

        _pending.AddRange(types
            .Where(IsConcreteMessage)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal));
    }

    /// <param name="services">The service collection to add message metadata to.</param>
    /// <param name="mark">The value <see cref="Mark"/> returned before this scan added its types.</param>
    /// <param name="existing">The registrations this scan is working against.</param>
    /// <param name="hasOpenNotificationHandlers">
    /// Whether any open generic notification handler is registered, which routes every notification.
    /// </param>
    /// <param name="openNotificationHandlersChanged">
    /// Whether <paramref name="hasOpenNotificationHandlers"/> became true during this scan.
    /// </param>
    internal void Register(
        IServiceCollection services,
        int mark,
        ExistingRegistrations existing,
        bool hasOpenNotificationHandlers,
        bool openNotificationHandlersChanged)
    {
        var routingChanged = _lastHandledMessageTypes is null ||
            openNotificationHandlersChanged ||
            !existing.HandledMessageTypes.SetEquals(_lastHandledMessageTypes);
        var startIndex = routingChanged ? 0 : mark;

        // Resolved types are compacted out of the pending list rather than removed one by one.
        var remaining = startIndex;
        for (var index = startIndex; index < _pending.Count; index++)
        {
            var messageType = _pending[index];
            if (existing.HandledMessageTypes.Contains(messageType) ||
                existing.RegisteredMessageTypes.Contains(messageType))
            {
                continue;
            }

            if (!IsRoutable(messageType, existing.HandledMessageTypes, hasOpenNotificationHandlers))
            {
                _pending[remaining++] = messageType;
                continue;
            }

            services.AddSingleton(new MessageRegistration(messageType));
            existing.RegisteredMessageTypes.Add(messageType);
        }

        _pending.RemoveRange(remaining, _pending.Count - remaining);
        _lastHandledMessageTypes = existing.HandledMessageTypes;
    }

    private static bool IsRoutable(
        Type messageType,
        IReadOnlySet<Type> handledMessageTypes,
        bool hasOpenNotificationHandlers)
    {
        if (hasOpenNotificationHandlers && typeof(INotification).IsAssignableFrom(messageType))
        {
            return true;
        }

        for (var current = messageType.BaseType; current is not null; current = current.BaseType)
        {
            if (handledMessageTypes.Contains(current))
            {
                return true;
            }
        }

        return messageType.GetInterfaces().Any(handledMessageTypes.Contains);
    }

    private static bool IsConcreteMessage(Type type) =>
        type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false } &&
        (typeof(IRequest).IsAssignableFrom(type) ||
         typeof(INotification).IsAssignableFrom(type));
}