using System.Reflection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Tracks concrete route targets discovered by assembly scanning.
/// </summary>
/// <remarks>
/// Routable targets are retained directly instead of becoming individual service descriptors.
/// Unroutable targets remain pending because a handler registered later may make them routable.
/// </remarks>
internal sealed class MessageRouteTargets
{
    private readonly HashSet<Assembly> _scannedAssemblies = [];
    private readonly List<Type> _pending = [];
    private readonly List<Type> _routable = [];
    private HashSet<Type>? _lastHandledMessageTypes;
    private bool _lastHasOpenNotificationHandlers;

    internal bool NeedsScan(Assembly assembly) => !_scannedAssemblies.Contains(assembly);

    /// <summary>
    /// Gets the known routable targets and any pending targets that final registrations may have made
    /// routable since the last scan.
    /// </summary>
    /// <param name="handlers">Every handler registration the registry is being created from.</param>
    /// <remarks>
    /// Registration methods called after the last scan can make a pending message routable, and no
    /// scan runs afterwards to notice. Registry creation therefore reconsiders these against the
    /// final registrations, which is what keeps routing independent of registration order.
    /// Reconsidering a message never asserts that it routes: route creation drops it exactly as
    /// before when it still does not.
    /// </remarks>
    internal IEnumerable<Type> GetRouteTargets(IEnumerable<HandlerRegistration> handlers)
    {
        foreach (var messageType in _routable)
        {
            yield return messageType;
        }

        if (_pending.Count == 0 || _lastHandledMessageTypes is null)
        {
            yield break;
        }

        var handledMessageTypes = new HashSet<Type>();
        var hasOpenNotificationHandlers = false;
        foreach (var handler in handlers)
        {
            if (handler is NotificationHandlerRegistration { IsOpenGeneric: true })
            {
                hasOpenNotificationHandlers = true;
            }
            else
            {
                handledMessageTypes.Add(handler.MessageType);
            }
        }

        if (hasOpenNotificationHandlers == _lastHasOpenNotificationHandlers &&
            handledMessageTypes.SetEquals(_lastHandledMessageTypes))
        {
            yield break;
        }

        foreach (var messageType in _pending)
        {
            yield return messageType;
        }
    }

    /// <summary>
    /// Marks the current end of the pending list, so that <see cref="Update"/> can tell the message
    /// types this scan adds from the ones earlier scans left unroutable.
    /// </summary>
    internal int MarkPending() => _pending.Count;

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

    /// <param name="mark">The value <see cref="MarkPending"/> returned before this scan added its types.</param>
    /// <param name="existing">The registrations this scan is working against.</param>
    internal void Update(
        int mark,
        ExistingRegistrations existing)
    {
        var routingChanged = _lastHandledMessageTypes is null ||
            existing.HasOpenNotificationHandler != _lastHasOpenNotificationHandlers ||
            !existing.HandledMessageTypes.SetEquals(_lastHandledMessageTypes);
        var startIndex = routingChanged ? 0 : mark;

        // Resolved types are compacted out of the pending list rather than removed one by one.
        var remaining = startIndex;
        for (var index = startIndex; index < _pending.Count; index++)
        {
            var messageType = _pending[index];
            if (existing.HandledMessageTypes.Contains(messageType))
            {
                continue;
            }

            if (!IsRoutable(
                    messageType,
                    existing.HandledMessageTypes,
                    existing.HasOpenNotificationHandler))
            {
                _pending[remaining++] = messageType;
                continue;
            }

            _routable.Add(messageType);
        }

        _pending.RemoveRange(remaining, _pending.Count - remaining);
        _lastHandledMessageTypes = existing.HandledMessageTypes;
        _lastHasOpenNotificationHandlers = existing.HasOpenNotificationHandler;
    }

    private static bool IsRoutable(
        Type messageType,
        HashSet<Type> handledMessageTypes,
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