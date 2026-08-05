namespace Dispatcher;

/// <summary>
/// Handles result-bearing commands of type <typeparamref name="TCommand"/>.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles a command and returns its response.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The command response.</returns>
    ValueTask<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handles resultless commands of type <typeparamref name="TCommand"/>.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Handles a command.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>An operation that represents command handling.</returns>
    ValueTask HandleAsync(TCommand command, CancellationToken cancellationToken);
}