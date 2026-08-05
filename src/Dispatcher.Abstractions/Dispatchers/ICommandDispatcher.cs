namespace Dispatcher;

/// <summary>
/// Dispatches commands to their registered handlers.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Executes a command and returns its response.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The command response.</returns>
    ValueTask<TResponse> ExecuteAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a command that does not return a response.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>An operation that represents command execution.</returns>
    ValueTask ExecuteAsync(
        ICommand command,
        CancellationToken cancellationToken = default);
}