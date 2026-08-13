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
    /// Gets the concrete handler type.
    /// </summary>
    /// <value>The concrete type of the registered handler.</value>
    public Type HandlerType { get; }
}