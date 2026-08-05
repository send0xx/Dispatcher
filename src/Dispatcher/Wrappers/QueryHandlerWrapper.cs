namespace Dispatcher;

internal abstract class QueryHandlerWrapper<TResponse> : RequestHandlerWrapper
{
    public abstract ValueTask<TResponse> HandleAsync(
        IQuery<TResponse> query,
        IServiceProvider serviceProvider,
        PipelineCache pipelineCache,
        CancellationToken cancellationToken);
}

internal sealed class QueryHandlerWrapper<TQuery, TResponse>(PipelineMode pipelineMode) : QueryHandlerWrapper<TResponse>
    where TQuery : IQuery<TResponse>
{
    public override ValueTask<TResponse> HandleAsync(
        IQuery<TResponse> query,
        IServiceProvider serviceProvider,
        PipelineCache pipelineCache,
        CancellationToken cancellationToken)
    {
        var typedQuery = (TQuery)query;

        if (pipelineMode == PipelineMode.None)
        {
            return serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResponse>>()
                .HandleAsync(typedQuery, cancellationToken);
        }

        if (pipelineMode == PipelineMode.Reusable)
        {
            var pipeline = pipelineCache.GetOrAdd(
                this,
                static (wrapper, provider) => wrapper.CreateReusablePipeline(provider));
            return pipeline(typedQuery, cancellationToken);
        }

        var handler = serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResponse>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TQuery, TResponse>>();

        if (behaviors.Count == 0)
        {
            return handler.HandleAsync(typedQuery, cancellationToken);
        }

        return HandleDynamicPipelineAsync(typedQuery, handler, behaviors, cancellationToken);
    }

    private static ValueTask<TResponse> HandleDynamicPipelineAsync(
        TQuery query,
        IQueryHandler<TQuery, TResponse> handler,
        IReadOnlyList<IPipelineBehavior<TQuery, TResponse>> behaviors,
        CancellationToken cancellationToken)
    {
        RequestHandlerDelegate<TQuery, TResponse> pipeline = (request, token) => handler.HandleAsync(request, token);

        for (var index = behaviors.Count - 1; index >= 1; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = (request, token) => behavior.HandleAsync(request, next, token);
        }

        return behaviors[0].HandleAsync(query, pipeline, cancellationToken);
    }

    private RequestHandlerDelegate<TQuery, TResponse> CreateReusablePipeline(IServiceProvider serviceProvider)
    {
        RequestHandlerDelegate<TQuery, TResponse> pipeline = (query, token) =>
            serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResponse>>()
                .HandleAsync(query, token);

        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TQuery, TResponse>>();
        for (var index = behaviors.Count - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = (query, token) => behavior.HandleAsync(query, next, token);
        }

        return pipeline;
    }
}