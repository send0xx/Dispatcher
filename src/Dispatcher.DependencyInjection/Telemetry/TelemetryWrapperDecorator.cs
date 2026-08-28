using System.Diagnostics.CodeAnalysis;
using Dispatcher.DependencyInjection;

namespace Dispatcher;

internal static class TelemetryWrapperDecorator
{
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal static RequestHandlerWrapper Decorate(
        RequestHandlerWrapper wrapper,
        RequestHandlerDescriptor registration,
        Type dispatchedMessageType,
        DispatcherTelemetry telemetry)
        => registration switch
        {
            QueryHandlerDescriptor query => CreateGenericDecorator(
                typeof(TelemetryQueryHandlerWrapper<>),
                query.ResponseType,
                wrapper,
                telemetry.CreateRoute(dispatchedMessageType, "query", "query")),
            CommandWithResponseHandlerDescriptor command => CreateGenericDecorator(
                typeof(TelemetryCommandWithResponseHandlerWrapper<>),
                command.ResponseType,
                wrapper,
                telemetry.CreateRoute(dispatchedMessageType, "execute", "command")),
            CommandHandlerDescriptor => new TelemetryCommandHandlerWrapper(
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

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
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