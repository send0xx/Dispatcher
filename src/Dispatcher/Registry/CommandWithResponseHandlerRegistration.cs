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

    /// <summary>
    /// Creates a result-bearing command handler registration.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to register.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the command.</typeparam>
    /// <typeparam name="THandler">The type of command handler to register.</typeparam>
    /// <returns>A command handler registration for the specified types.</returns>
    public static CommandWithResponseHandlerRegistration Create<TCommand, TResponse, THandler>()
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse> =>
        new(typeof(TCommand), typeof(TResponse), typeof(THandler));
}