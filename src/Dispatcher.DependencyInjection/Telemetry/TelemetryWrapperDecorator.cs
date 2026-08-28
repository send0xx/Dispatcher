using System.Diagnostics.CodeAnalysis;
using Dispatcher.DependencyInjection;

namespace Dispatcher;

internal static class TelemetryWrapperDecorator
{
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal static RequestHandlerWrapper DecorateQuery(
        RequestHandlerWrapper wrapper,
        Type responseType,
        Type dispatchedMessageType,
        DispatcherTelemetry telemetry) =>
        CreateGenericDecorator(
            typeof(TelemetryQueryHandlerWrapper<>),
            responseType,
            wrapper,
            telemetry.CreateRoute(dispatchedMessageType, "query", "query"));

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal static RequestHandlerWrapper DecorateCommandWithResponse(
        RequestHandlerWrapper wrapper,
        Type responseType,
        Type dispatchedMessageType,
        DispatcherTelemetry telemetry) =>
        CreateGenericDecorator(
            typeof(TelemetryCommandWithResponseHandlerWrapper<>),
            responseType,
            wrapper,
            telemetry.CreateRoute(dispatchedMessageType, "execute", "command"));

    internal static RequestHandlerWrapper DecorateCommand(
        RequestHandlerWrapper wrapper,
        Type dispatchedMessageType,
        DispatcherTelemetry telemetry) =>
        new TelemetryCommandHandlerWrapper(
            (CommandHandlerWrapperBase)wrapper,
            telemetry.CreateRoute(dispatchedMessageType, "execute", "command"));

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