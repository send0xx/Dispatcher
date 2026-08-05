namespace Dispatcher;

public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest
{
    ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken);
}