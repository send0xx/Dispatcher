namespace Dispatcher;

internal abstract class CommandWithResponseHandlerWrapper<TResponse> : RequestHandlerWrapper
{
    public abstract ValueTask<TResponse> HandleAsync(
        ICommand<TResponse> command,
        IServiceProvider serviceProvider,
        PipelineCache pipelineCache,
        CancellationToken cancellationToken);
}

internal sealed class CommandWithResponseHandlerWrapper<TCommand, TResponse>(PipelineMode pipelineMode)
    : CommandWithResponseHandlerWrapper<TResponse>
    where TCommand : ICommand<TResponse>
{
    public override ValueTask<TResponse> HandleAsync(
        ICommand<TResponse> command,
        IServiceProvider serviceProvider,
        PipelineCache pipelineCache,
        CancellationToken cancellationToken)
    {
        var typedCommand = (TCommand)command;

        if (pipelineMode == PipelineMode.None)
        {
            return serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResponse>>()
                .HandleAsync(typedCommand, cancellationToken);
        }

        if (pipelineMode == PipelineMode.Reusable)
        {
            var pipeline = pipelineCache.GetOrAdd(
                this,
                static (wrapper, provider) => wrapper.CreateReusablePipeline(provider));
            return pipeline(typedCommand, cancellationToken);
        }

        var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResponse>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TCommand, TResponse>>();

        if (behaviors.Count == 0)
        {
            return handler.HandleAsync(typedCommand, cancellationToken);
        }

        return HandleDynamicPipelineAsync(typedCommand, handler, behaviors, cancellationToken);
    }

    private static ValueTask<TResponse> HandleDynamicPipelineAsync(
        TCommand command,
        ICommandHandler<TCommand, TResponse> handler,
        IReadOnlyList<IPipelineBehavior<TCommand, TResponse>> behaviors,
        CancellationToken cancellationToken)
    {
        RequestHandlerDelegate<TCommand, TResponse> pipeline = (request, token) => handler.HandleAsync(request, token);

        for (var index = behaviors.Count - 1; index >= 1; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = (request, token) => behavior.HandleAsync(request, next, token);
        }

        return behaviors[0].HandleAsync(command, pipeline, cancellationToken);
    }

    private RequestHandlerDelegate<TCommand, TResponse> CreateReusablePipeline(IServiceProvider serviceProvider)
    {
        RequestHandlerDelegate<TCommand, TResponse> pipeline = (command, token) =>
            serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResponse>>()
                .HandleAsync(command, token);

        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TCommand, TResponse>>();
        for (var index = behaviors.Count - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = (command, token) => behavior.HandleAsync(command, next, token);
        }

        return pipeline;
    }
}

internal abstract class CommandHandlerWrapperBase : RequestHandlerWrapper
{
    public abstract ValueTask HandleAsync(
        ICommand command,
        IServiceProvider serviceProvider,
        PipelineCache pipelineCache,
        CancellationToken cancellationToken);
}

internal sealed class CommandHandlerWrapper<TCommand>(PipelineMode pipelineMode) : CommandHandlerWrapperBase
    where TCommand : ICommand
{
    public override ValueTask HandleAsync(
        ICommand command,
        IServiceProvider serviceProvider,
        PipelineCache pipelineCache,
        CancellationToken cancellationToken)
    {
        var typedCommand = (TCommand)command;

        if (pipelineMode == PipelineMode.None)
        {
            return serviceProvider.GetRequiredService<ICommandHandler<TCommand>>()
                .HandleAsync(typedCommand, cancellationToken);
        }

        if (pipelineMode == PipelineMode.Reusable)
        {
            var pipeline = pipelineCache.GetOrAdd(
                this,
                static (wrapper, provider) => wrapper.CreateReusablePipeline(provider));
            return ExecuteReusablePipelineAsync(pipeline, typedCommand, cancellationToken);
        }

        var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TCommand, Unit>>();

        if (behaviors.Count == 0)
        {
            return handler.HandleAsync(typedCommand, cancellationToken);
        }

        return HandlePipelineAsync(typedCommand, handler, behaviors, cancellationToken);
    }

    private static async ValueTask HandlePipelineAsync(
        TCommand command,
        ICommandHandler<TCommand> handler,
        IReadOnlyList<IPipelineBehavior<TCommand, Unit>> behaviors,
        CancellationToken cancellationToken)
    {
        RequestHandlerDelegate<TCommand, Unit> pipeline = async (request, token) =>
        {
            await handler.HandleAsync(request, token).ConfigureAwait(false);
            return Unit.Value;
        };

        for (var index = behaviors.Count - 1; index >= 1; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = (request, token) => behavior.HandleAsync(request, next, token);
        }

        await behaviors[0].HandleAsync(command, pipeline, cancellationToken).ConfigureAwait(false);
    }

    private RequestHandlerDelegate<TCommand, Unit> CreateReusablePipeline(IServiceProvider serviceProvider)
    {
        RequestHandlerDelegate<TCommand, Unit> pipeline = async (command, token) =>
        {
            await serviceProvider.GetRequiredService<ICommandHandler<TCommand>>()
                .HandleAsync(command, token)
                .ConfigureAwait(false);
            return Unit.Value;
        };

        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TCommand, Unit>>();
        for (var index = behaviors.Count - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = (command, token) => behavior.HandleAsync(command, next, token);
        }

        return pipeline;
    }

    private static async ValueTask ExecuteReusablePipelineAsync(
        RequestHandlerDelegate<TCommand, Unit> pipeline,
        TCommand command,
        CancellationToken cancellationToken)
    {
        await pipeline(command, cancellationToken).ConfigureAwait(false);
    }
}