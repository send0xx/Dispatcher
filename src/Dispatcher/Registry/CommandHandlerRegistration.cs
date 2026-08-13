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

    /// <summary>
    /// Creates a resultless command handler registration.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to register.</typeparam>
    /// <typeparam name="THandler">The type of command handler to register.</typeparam>
    /// <returns>A command handler registration for the specified types.</returns>
    public static CommandHandlerRegistration Create<TCommand, THandler>()
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand> =>
        new(typeof(TCommand), typeof(THandler));
}