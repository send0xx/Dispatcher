using System.Diagnostics.CodeAnalysis;

namespace Dispatcher;

internal static class TelemetryWrapperDecorator
{
    [RequiresDynamicCode("Creating telemetry wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating telemetry wrappers from registration metadata is not trimming safe.")]
    internal static RequestHandlerWrapper Decorate(
        RequestHandlerWrapper wrapper,
        HandlerRegistration registration,
        Type dispatchedMessageType,
        DispatcherTelemetry telemetry)
        => registration switch
        {
            QueryHandlerRegistration query => CreateGenericDecorator(
                typeof(TelemetryQueryHandlerWrapper<>),
                query.ResponseType,
                wrapper,
                telemetry.CreateRoute(dispatchedMessageType, "query", "query")),
            CommandWithResponseHandlerRegistration command => CreateGenericDecorator(
                typeof(TelemetryCommandWithResponseHandlerWrapper<>),
                command.ResponseType,
                wrapper,
                telemetry.CreateRoute(dispatchedMessageType, "execute", "command")),
            CommandHandlerRegistration => new TelemetryCommandHandlerWrapper(
                (CommandHandlerWrapperBase)wrapper,
                telemetry.CreateRoute(dispatchedMessageType, "execute", "command")),
            _ => throw new ArgumentOutOfRangeException(
                nameof(registration),
                registration.GetType(),
                "Unsupported request handler registration type.")
        };

    internal static NotificationHandlerWrapper Decorate(
        NotificationHandlerWrapper wrapper,
        Type dispatchedMessageType,
        DispatcherTelemetry telemetry) =>
        new TelemetryNotificationHandlerWrapper(
            wrapper,
            telemetry.CreateRoute(dispatchedMessageType, "publish", "notification"));

    [RequiresDynamicCode("Creating telemetry wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating telemetry wrappers from registration metadata is not trimming safe.")]
    private static RequestHandlerWrapper CreateGenericDecorator(
        Type decoratorType,
        Type responseType,
        RequestHandlerWrapper wrapper,
        DispatcherTelemetryRoute route) =>
        (RequestHandlerWrapper)Activator.CreateInstance(
            decoratorType.MakeGenericType(responseType),
            wrapper,
            route)!;
}