namespace Dispatcher;

/// <summary>
/// Dispatches queries to their registered handlers.
/// </summary>
public interface IQueryDispatcher
{
    /// <summary>
    /// Dispatches a query and returns its response.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The query response.</returns>
    ValueTask<TResponse> QueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default);
}