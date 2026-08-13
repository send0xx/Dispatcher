namespace Dispatcher;

/// <summary>
/// Represents a behavior in the execution pipeline for a query or command.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the pipeline.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest
{
    /// <summary>
    /// Handles the request and controls whether the next pipeline component is invoked.
    /// </summary>
    /// <param name="request">The request being dispatched.</param>
    /// <param name="next">The delegate that invokes the next behavior or the request handler.</param>
    /// <param name="cancellationToken">The cancellation token for this behavior.</param>
    /// <returns>A value task whose result contains the pipeline response.</returns>
    /// <remarks>
    /// A behavior can short-circuit the pipeline by returning without invoking <paramref name="next"/>.
    /// </remarks>
    ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}