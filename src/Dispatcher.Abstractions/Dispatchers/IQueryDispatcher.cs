namespace Dispatcher;

/// <summary>
/// Defines operations for dispatching queries to their registered handlers.
/// </summary>
public interface IQueryDispatcher
{
    /// <summary>
    /// Dispatches a query and returns its response.
    /// </summary>
    /// <typeparam name="TResponse">The type of response returned by the query.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A value task whose result contains the response returned by the query handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="T:Dispatcher.HandlerNotFoundException">
    /// No handler is registered for the concrete type of <paramref name="query"/>.
    /// </exception>
    ValueTask<TResponse> QueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default);
}