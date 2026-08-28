namespace Dispatcher;

/// <summary>
/// Defines an executable response-bearing command route for a response type.
/// </summary>
/// <typeparam name="TResponse">The command response type.</typeparam>
internal abstract class CommandWithResponseHandlerWrapper<TResponse> : RequestHandlerWrapper
{
    public abstract ValueTask<TResponse> HandleAsync(
        ICommand<TResponse> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves and invokes the selected response-bearing command handler, applying compatible pipeline
/// behaviors when present.
/// </summary>
/// <typeparam name="TCommand">The selected handled command type.</typeparam>
/// <typeparam name="TResponse">The command response type.</typeparam>
internal sealed class
    CommandWithResponseHandlerWrapper<TCommand, TResponse> : CommandWithResponseHandlerWrapper<TResponse>
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

/// <summary>
/// Defines an executable resultless command route.
/// </summary>
internal abstract class CommandHandlerWrapperBase : RequestHandlerWrapper
{
    public abstract ValueTask HandleAsync(
        ICommand command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves and invokes the selected resultless command handler, adapting its pipeline response to
/// <see cref="Unit"/> and applying compatible pipeline behaviors when present.
/// </summary>
/// <typeparam name="TCommand">The selected handled command type.</typeparam>
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

/// <summary>
/// Records telemetry around an executable response-bearing command route.
/// </summary>
/// <param name="inner">The prepared response-bearing command route to invoke.</param>
/// <param name="route">The telemetry route associated with the concrete dispatched command type.</param>
/// <typeparam name="TResponse">The command response type.</typeparam>
internal sealed class TelemetryCommandWithResponseHandlerWrapper<TResponse>(
    CommandWithResponseHandlerWrapper<TResponse> inner,
    DispatcherTelemetryRoute route) : CommandWithResponseHandlerWrapper<TResponse>
{
    public override async ValueTask<TResponse> HandleAsync(
        ICommand<TResponse> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var telemetryScope = route.Start();
        try
        {
            var result = await inner.HandleAsync(command, serviceProvider, cancellationToken).ConfigureAwait(false);
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

/// <summary>
/// Records telemetry around an executable resultless command route.
/// </summary>
/// <param name="inner">The prepared resultless command route to invoke.</param>
/// <param name="route">The telemetry route associated with the concrete dispatched command type.</param>
internal sealed class TelemetryCommandHandlerWrapper(
    CommandHandlerWrapperBase inner,
    DispatcherTelemetryRoute route) : CommandHandlerWrapperBase
{
    public override async ValueTask HandleAsync(
        ICommand command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var telemetryScope = route.Start();
        try
        {
            await inner.HandleAsync(command, serviceProvider, cancellationToken).ConfigureAwait(false);
            telemetryScope.Complete();
        }
        catch (Exception exception)
        {
            telemetryScope.Fail(exception);
            throw;
        }
    }
}