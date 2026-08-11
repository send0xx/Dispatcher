namespace Dispatcher;

internal abstract class QueryHandlerWrapper<TResponse> : RequestHandlerWrapper
{
    public abstract ValueTask<TResponse> HandleAsync(
        IQuery<TResponse> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class QueryHandlerWrapper<TQuery, TResponse> : QueryHandlerWrapper<TResponse>
    where TQuery : IQuery<TResponse>
{
    public override ValueTask<TResponse> HandleAsync(
        IQuery<TResponse> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var typedQuery = (TQuery)query;
        var handler = serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResponse>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TQuery, TResponse>>();

        if (behaviors.Count == 0)
        {
            return handler.HandleAsync(typedQuery, cancellationToken);
        }

        return HandlePipelineAsync(typedQuery, handler, behaviors, cancellationToken);
    }

    private static ValueTask<TResponse> HandlePipelineAsync(
        TQuery query,
        IQueryHandler<TQuery, TResponse> handler,
        IReadOnlyList<IPipelineBehavior<TQuery, TResponse>> behaviors,
        CancellationToken cancellationToken)
    {
        RequestHandlerDelegate<TResponse> pipeline = token => handler.HandleAsync(query, token);

        for (var index = behaviors.Count - 1; index >= 1; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = token => behavior.HandleAsync(query, next, token);
        }

        return behaviors[0].HandleAsync(query, pipeline, cancellationToken);
    }
}

internal sealed class TelemetryQueryHandlerWrapper<TResponse>(
    QueryHandlerWrapper<TResponse> inner,
    DispatcherTelemetryRoute route) : QueryHandlerWrapper<TResponse>
{
    public override async ValueTask<TResponse> HandleAsync(
        IQuery<TResponse> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var telemetryScope = route.Start();
        try
        {
            var result = await inner.HandleAsync(query, serviceProvider, cancellationToken)
                .ConfigureAwait(false);
            telemetryScope.Complete();
            return result;
        }
        catch (Exception exception)
        {
            telemetryScope.Fail(exception);
            throw;
        }
    }
}