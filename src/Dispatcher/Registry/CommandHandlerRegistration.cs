namespace Dispatcher;

/// <summary>
/// Describes a resultless command handler registration.
/// </summary>
public sealed record CommandHandlerRegistration : HandlerRegistration
{
    /// <summary>
    /// Initializes a resultless command handler registration.
    /// </summary>
    /// <param name="messageType">The handled command type.</param>
    /// <param name="handlerType">The concrete handler type.</param>
    public CommandHandlerRegistration(Type messageType, Type handlerType)
        : base(messageType, handlerType)
    {
    }

    internal RequestHandlerWrapperFactory? WrapperFactory { get; init; }

    /// <summary>
    /// Creates a resultless command handler registration.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="THandler">The command handler implementation type.</typeparam>
    /// <returns>The command handler registration.</returns>
    public static CommandHandlerRegistration Create<TCommand, THandler>()
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand> =>
        new(typeof(TCommand), typeof(THandler))
        {
            WrapperFactory = new CommandHandlerWrapperFactory<TCommand>()
        };
}
