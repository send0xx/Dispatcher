namespace Dispatcher;

internal sealed class PipelineCache(IServiceProvider serviceProvider)
{
    private Dictionary<RequestHandlerWrapper, object>? _pipelines;

    public TPipeline GetOrAdd<TWrapper, TPipeline>(
        TWrapper wrapper,
        Func<TWrapper, IServiceProvider, TPipeline> factory)
        where TWrapper : RequestHandlerWrapper
        where TPipeline : class
    {
        lock (this)
        {
            _pipelines ??= [];

            if (_pipelines.TryGetValue(wrapper, out var pipeline))
            {
                return (TPipeline)pipeline;
            }

            var created = factory(wrapper, serviceProvider);
            _pipelines.Add(wrapper, created);
            return created;
        }
    }
}