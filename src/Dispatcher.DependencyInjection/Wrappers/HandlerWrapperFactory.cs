namespace Dispatcher;

internal abstract class RequestHandlerWrapperFactory
{
    internal abstract RequestHandlerWrapper Create(DispatcherTelemetry? telemetry);
}

internal sealed class QueryHandlerWrapperFactory<TQuery, TResponse> : RequestHandlerWrapperFactory
    where TQuery : IQuery<TResponse>
{
    internal override RequestHandlerWrapper Create(DispatcherTelemetry? telemetry)
    {
        QueryHandlerWrapper<TResponse> wrapper = new QueryHandlerWrapper<TQuery, TResponse>();
        return telemetry is null
            ? wrapper
            : new TelemetryQueryHandlerWrapper<TResponse>(
                wrapper,
                telemetry.CreateRoute(typeof(TQuery), "query", "query"));
    }
}

internal sealed class CommandWithResponseHandlerWrapperFactory<TCommand, TResponse>
    : RequestHandlerWrapperFactory
    where TCommand : ICommand<TResponse>
{
    internal override RequestHandlerWrapper Create(DispatcherTelemetry? telemetry)
    {
        CommandWithResponseHandlerWrapper<TResponse> wrapper =
            new CommandWithResponseHandlerWrapper<TCommand, TResponse>();
        return telemetry is null
            ? wrapper
            : new TelemetryCommandWithResponseHandlerWrapper<TResponse>(
                wrapper,
                telemetry.CreateRoute(typeof(TCommand), "execute", "command"));
    }
}

internal sealed class CommandHandlerWrapperFactory<TCommand> : RequestHandlerWrapperFactory
    where TCommand : ICommand
{
    internal override RequestHandlerWrapper Create(DispatcherTelemetry? telemetry)
    {
        CommandHandlerWrapperBase wrapper = new CommandHandlerWrapper<TCommand>();
        return telemetry is null
            ? wrapper
            : new TelemetryCommandHandlerWrapper(
                wrapper,
                telemetry.CreateRoute(typeof(TCommand), "execute", "command"));
    }
}

internal abstract class NotificationHandlerWrapperFactory
{
    internal abstract NotificationHandlerWrapper Create(DispatcherTelemetry? telemetry);
}

internal sealed class NotificationHandlerWrapperFactory<TNotification>
    : NotificationHandlerWrapperFactory
    where TNotification : INotification
{
    internal override NotificationHandlerWrapper Create(DispatcherTelemetry? telemetry)
    {
        NotificationHandlerWrapper wrapper = new NotificationHandlerWrapper<TNotification>();
        return telemetry is null
            ? wrapper
            : new TelemetryNotificationHandlerWrapper(
                wrapper,
                telemetry.CreateRoute(typeof(TNotification), "publish", "notification"));
    }
}