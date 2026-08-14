namespace Dispatcher;

/// <summary>
/// Represents a handler registration for a command that returns a response.
/// </summary>
public sealed record CommandWithResponseHandlerRegistration : HandlerRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandWithResponseHandlerRegistration"/> class.
    /// </summary>
    /// <param name="messageType">The handled command type.</param>
    /// <param name="responseType">The command response type.</param>
    /// <param name="handlerType">The concrete handler type.</param>
    public CommandWithResponseHandlerRegistration(Type messageType, Type responseType, Type handlerType)
        : base(messageType, handlerType)
    {
        ResponseType = responseType;
    }

    /// <summary>
    /// Gets the command response type.
    /// </summary>
    /// <value>The type of response returned by the command.</value>
    public Type ResponseType { get; }
}