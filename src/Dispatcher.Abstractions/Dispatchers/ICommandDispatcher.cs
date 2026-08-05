namespace Dispatcher;

public interface ICommandDispatcher
{
    ValueTask<TResponse> ExecuteAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default);

    ValueTask ExecuteAsync(
        ICommand command,
        CancellationToken cancellationToken = default);
}