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
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TQuery, TResponse>>().ToArray();
        RequestHandlerDelegate<TResponse> pipeline = token => handler.HandleAsync(typedQuery, token);

        for (var index = behaviors.Length - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = token => behavior.HandleAsync(typedQuery, next, token);
        }

        return pipeline(cancellationToken);
    }
}