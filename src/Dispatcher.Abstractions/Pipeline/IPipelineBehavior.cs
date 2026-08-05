namespace Dispatcher;

/// <summary>
/// Represents middleware around a query or command handler.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest
{
    /// <summary>
    /// Handles a request and optionally invokes the next pipeline component.
    /// </summary>
    /// <param name="request">The request being dispatched.</param>
    /// <param name="next">Invokes the next behavior or the request handler.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The pipeline response.</returns>
    ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}