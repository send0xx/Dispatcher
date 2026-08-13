namespace Dispatcher;

/// <summary>
/// Defines a handler for queries of type <typeparamref name="TQuery"/>.
/// </summary>
/// <typeparam name="TQuery">The type of query to handle.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Handles a query and returns its response.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A value task whose result contains the query response.</returns>
    ValueTask<TResponse> HandleAsync(
        TQuery query,
        CancellationToken cancellationToken = default);
}