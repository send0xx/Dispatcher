namespace Dispatcher;

public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest
{
    ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}