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
    /// Gets the handled message type.
    /// </summary>
    /// <value>The concrete type of the handled message.</value>
    public Type MessageType { get; }

    /// <summary>
    /// Gets the concrete handler type.
    /// </summary>
    /// <value>The concrete type of the registered handler.</value>
    public Type HandlerType { get; }
}