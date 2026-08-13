namespace Dispatcher;

/// <summary>
/// Defines a handler for commands of type <typeparamref name="TCommand"/> that return a response.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles a command and returns its response.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A value task whose result contains the command response.</returns>
    ValueTask<TResponse> HandleAsync(
        TCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a handler for commands of type <typeparamref name="TCommand"/> that do not return a response.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle.</typeparam>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Handles a command.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A value task that represents the asynchronous command handling operation.</returns>
    ValueTask HandleAsync(
        TCommand command,
        CancellationToken cancellationToken = default);
}