namespace Dispatcher;

internal abstract class CommandWithResponseHandlerWrapper<TResponse> : RequestHandlerWrapper
{
    public abstract ValueTask<TResponse> HandleAsync(
        ICommand<TResponse> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class CommandWithResponseHandlerWrapper<TCommand, TResponse> : CommandWithResponseHandlerWrapper<TResponse>
    where TCommand : ICommand<TResponse>
{
    public override ValueTask<TResponse> HandleAsync(
        ICommand<TResponse> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var typedCommand = (TCommand)command;
        var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResponse>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TCommand, TResponse>>();

        if (behaviors.Count == 0)
        {
            return handler.HandleAsync(typedCommand, cancellationToken);
        }

        return HandlePipelineAsync(typedCommand, handler, behaviors, cancellationToken);
    }

    private static ValueTask<TResponse> HandlePipelineAsync(
        TCommand command,
        ICommandHandler<TCommand, TResponse> handler,
        IReadOnlyList<IPipelineBehavior<TCommand, TResponse>> behaviors,
        CancellationToken cancellationToken)
    {
        RequestHandlerDelegate<TResponse> pipeline = token => handler.HandleAsync(command, token);

        for (var index = behaviors.Count - 1; index >= 1; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = token => behavior.HandleAsync(command, next, token);
        }

        return behaviors[0].HandleAsync(command, pipeline, cancellationToken);
    }
}

internal abstract class CommandHandlerWrapperBase : RequestHandlerWrapper
{
    public abstract ValueTask HandleAsync(
        ICommand command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class CommandHandlerWrapper<TCommand> : CommandHandlerWrapperBase
    where TCommand : ICommand
{
    public override ValueTask HandleAsync(
        ICommand command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var typedCommand = (TCommand)command;
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
        RequestHandlerDelegate<Unit> pipeline = async token =>
        {
            await handler.HandleAsync(command, token).ConfigureAwait(false);
            return Unit.Value;
        };

        for (var index = behaviors.Count - 1; index >= 1; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = token => behavior.HandleAsync(command, next, token);
        }

        await behaviors[0].HandleAsync(command, pipeline, cancellationToken).ConfigureAwait(false);
    }
}