namespace Dispatcher.NativeAotHostSample;

internal sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Dispatching {RequestType}", typeof(TRequest).Name);
        var response = await next(cancellationToken);
        logger.LogInformation("Dispatched {RequestType}", typeof(TRequest).Name);
        return response;
    }
}