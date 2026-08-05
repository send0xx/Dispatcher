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
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TCommand, TResponse>>().ToArray();
        RequestHandlerDelegate<TResponse> pipeline = token => handler.HandleAsync(typedCommand, token);

        for (var index = behaviors.Length - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = token => behavior.HandleAsync(typedCommand, next, token);
        }

        return pipeline(cancellationToken);
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
    public override async ValueTask HandleAsync(
        ICommand command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var typedCommand = (TCommand)command;
        var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TCommand, Unit>>().ToArray();
        RequestHandlerDelegate<Unit> pipeline = async token =>
        {
            await handler.HandleAsync(typedCommand, token).ConfigureAwait(false);
            return Unit.Value;
        };

        for (var index = behaviors.Length - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;
            pipeline = token => behavior.HandleAsync(typedCommand, next, token);
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
    }
}