namespace Dispatcher;

/// <summary>
/// Represents a handler registration for a command that does not return a response.
/// </summary>
public sealed record CommandHandlerRegistration : HandlerRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHandlerRegistration"/> class.
    /// </summary>
    /// <param name="messageType">The handled command type.</param>
    /// <param name="handlerType">The concrete handler type.</param>
    public CommandHandlerRegistration(Type messageType, Type handlerType)
        : base(messageType, handlerType)
    {
    }
}