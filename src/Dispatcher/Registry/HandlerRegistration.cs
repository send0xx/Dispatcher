namespace Dispatcher;

/// <summary>
/// Represents common metadata for a handler registration.
/// </summary>
public abstract record HandlerRegistration : MessageRegistration
{
    internal HandlerRegistration(Type messageType, Type handlerType)
        : base(messageType)
    {
        HandlerType = handlerType;
    }

    /// <summary>
    /// Gets the handler type.
    /// </summary>
    /// <value>The closed handler type or open generic notification handler type definition.</value>
    public Type HandlerType { get; }
}