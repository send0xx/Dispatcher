namespace Dispatcher;

public interface IQueryDispatcher
{
    ValueTask<TResponse> QueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default);
}