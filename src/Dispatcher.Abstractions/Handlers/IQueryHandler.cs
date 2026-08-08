namespace Dispatcher;

/// <summary>
/// Handles queries of type <typeparamref name="TQuery"/>.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Handles a query and returns its response.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The query response.</returns>
    ValueTask<TResponse> HandleAsync(
        TQuery query,
        CancellationToken cancellationToken = default);
}