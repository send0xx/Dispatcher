namespace Dispatcher;

/// <summary>
/// Defines operations for executing commands through their registered handlers.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Executes a command and returns its response.
    /// </summary>
    /// <typeparam name="TResponse">The type of response returned by the command.</typeparam>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A value task whose result contains the response returned by the command handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
    /// <exception cref="T:Dispatcher.HandlerNotFoundException">
    /// No handler is registered for the concrete type of <paramref name="command"/>.
    /// </exception>
    ValueTask<TResponse> ExecuteAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a command that does not return a response.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A value task that represents the asynchronous command execution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
    /// <exception cref="T:Dispatcher.HandlerNotFoundException">
    /// No handler is registered for the concrete type of <paramref name="command"/>.
    /// </exception>
    ValueTask ExecuteAsync(
        ICommand command,
        CancellationToken cancellationToken = default);
}