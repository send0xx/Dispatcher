namespace Dispatcher.DependencyInjection;

/// <summary>
/// The message types that have a handler, together with whether any open generic notification
/// handler is registered.
/// </summary>
/// <remarks>
/// Route targets are resolved against this set, and comparing two of them tells whether routing can
/// have changed since the last scan.
/// </remarks>
internal sealed class HandledMessages
{
    private readonly HashSet<Type> _messageTypes = [];

    /// <summary>
    /// Gets whether a handler that handles every notification satisfying its constraints is
    /// registered. Such a handler names no message type of its own.
    /// </summary>
    internal bool HasOpenNotificationHandler { get; private set; }

    internal static HandledMessages Read(IEnumerable<HandlerDescriptor> handlers)
    {
        var handled = new HandledMessages();
        foreach (var handler in handlers)
        {
            handled.Add(handler);
        }

        return handled;
    }

    internal void Add(HandlerDescriptor handler)
    {
        if (handler is NotificationHandlerDescriptor { IsOpenGeneric: true })
        {
            HasOpenNotificationHandler = true;
        }
        else
        {
            _messageTypes.Add(handler.MessageType);
        }
    }

    /// <summary>
    /// Determines whether the message type has a handler of its own.
    /// </summary>
    internal bool Contains(Type messageType) => _messageTypes.Contains(messageType);

    /// <summary>
    /// Determines whether a base class or an interface of the message type has a handler, which is
    /// what makes the message routable without a handler of its own.
    /// </summary>
    internal bool CanRouteBase(Type messageType)
    {
        if (HasOpenNotificationHandler && typeof(INotification).IsAssignableFrom(messageType))
        {
            return true;
        }

        foreach (var assignableType in MessageTypes.GetAssignableTypes(messageType))
        {
            if (assignableType != messageType && _messageTypes.Contains(assignableType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the same messages are handled, so that routing cannot have changed.
    /// </summary>
    internal bool Matches(HandledMessages? other) =>
        other is not null &&
        HasOpenNotificationHandler == other.HasOpenNotificationHandler &&
        _messageTypes.SetEquals(other._messageTypes);
}