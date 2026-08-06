namespace Dispatcher;

/// <summary>
/// Describes a result-bearing command handler registration.
/// </summary>
public sealed record CommandWithResponseHandlerRegistration : HandlerRegistration
{
    /// <summary>
    /// Initializes a result-bearing command handler registration.
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
    public Type ResponseType { get; }

    internal RequestHandlerWrapper? Wrapper { get; init; }

    /// <summary>
    /// Creates a result-bearing command handler registration.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The command handler implementation type.</typeparam>
    /// <returns>The command handler registration.</returns>
    public static CommandWithResponseHandlerRegistration Create<TCommand, TResponse, THandler>()
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse> =>
        new(typeof(TCommand), typeof(TResponse), typeof(THandler))
        {
            Wrapper = new CommandWithResponseHandlerWrapper<TCommand, TResponse>()
        };
}