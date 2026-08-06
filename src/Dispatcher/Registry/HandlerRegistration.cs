namespace Dispatcher;

/// <summary>
/// Provides common metadata for a handler registration.
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
    public Type MessageType { get; }

    /// <summary>
    /// Gets the concrete handler type.
    /// </summary>
    public Type HandlerType { get; }
}