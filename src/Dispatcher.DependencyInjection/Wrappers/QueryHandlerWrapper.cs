namespace Dispatcher;

/// <summary>
/// Defines an executable query route for a response type.
/// </summary>
/// <typeparam name="TResponse">The query response type.</typeparam>
internal abstract class QueryHandlerWrapper<TResponse> : RequestHandlerWrapper
{
    public abstract ValueTask<TResponse> HandleAsync(
        IQuery<TResponse> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves and invokes the selected query handler, applying compatible pipeline behaviors when present.
/// </summary>
/// <typeparam name="TQuery">The selected handled query type.</typeparam>
/// <typeparam name="TResponse">The query response type.</typeparam>
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

/// <summary>
/// Records telemetry around an executable query route.
/// </summary>
/// <param name="inner">The prepared query route to invoke.</param>
/// <param name="route">The telemetry route associated with the concrete dispatched query type.</param>
/// <typeparam name="TResponse">The query response type.</typeparam>
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