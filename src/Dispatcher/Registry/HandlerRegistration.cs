namespace Dispatcher;

/// <summary>
/// Represents common metadata for a handler registration.
/// </summary>
public abstract record HandlerRegistration
{
    internal HandlerRegistration(Type messageType, Type handlerType)
    {
        MessageType = messageType;
        HandlerType = handlerType;
    }

    /// <summary>
    /// Gets the handled message type or open notification handler type parameter.
    /// </summary>
    /// <value>The handled message type or open notification handler type parameter.</value>
    public Type MessageType { get; }

    /// <summary>
    /// Gets the handler type.
    /// </summary>
    /// <value>The closed handler type or open generic notification handler type definition.</value>
    public Type HandlerType { get; }
}