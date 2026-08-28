using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Dispatcher.DependencyInjection;

namespace Dispatcher;

/// <summary>
/// Creates the immutable registry the reflection-based Dispatcher routes through, from the handlers
/// a service collection registers and the concrete messages that may route to them.
/// </summary>
internal static class DispatcherRegistryFactory
{
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal static DispatcherRegistry Create(
        IEnumerable<HandlerDescriptor> handlers,
        IEnumerable<Type> routeTargets,
        DispatcherTelemetry? telemetry)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(routeTargets);

        var registrations = handlers.ToArray();
        var openNotificationRegistrations = registrations
            .OfType<NotificationHandlerDescriptor>()
            .Where(static registration => registration.IsOpenGeneric)
            .ToArray();

        // A handler always routes its own message type, whether or not scanning discovered it.
        var messageTypes = routeTargets
            .Concat(registrations.Select(static registration => registration.MessageType))
            .Distinct()
            .ToArray();
        var requests = CreateRequestRoutes(
            messageTypes,
            CreateRequestWrappers(registrations),
            telemetry);
        var notifications = CreateNotificationRoutes(
            messageTypes,
            CreateNotificationWrappers(registrations),
            openNotificationRegistrations,
            telemetry);

        return new DispatcherRegistry(requests.ToFrozenDictionary(), notifications.ToFrozenDictionary());
    }

    /// <summary>
    /// Creates one wrapper per handled query or command type, failing when two handlers claim the
    /// same message type.
    /// </summary>
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, RequestHandler> CreateRequestWrappers(
        IEnumerable<HandlerDescriptor> registrations)
    {
        var wrappers = new Dictionary<Type, RequestHandler>();
        foreach (var registration in registrations.OfType<RequestHandlerDescriptor>())
        {
            if (wrappers.TryGetValue(registration.MessageType, out var existing))
            {
                throw new DuplicateHandlerException(
                    registration.MessageType,
                    existing.Registration.HandlerType,
                    registration.HandlerType);
            }

            wrappers.Add(
                registration.MessageType,
                new RequestHandler(registration, registration.CreateWrapper()));
        }

        return wrappers;
    }

    /// <summary>
    /// Creates one wrapper per handled notification type. The wrapper resolves every handler of that
    /// type, so repeated registrations of the same notification share it.
    /// </summary>
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, NotificationHandlerWrapper> CreateNotificationWrappers(
        IEnumerable<HandlerDescriptor> registrations)
    {
        var wrappers = new Dictionary<Type, NotificationHandlerWrapper>();
        foreach (var registration in registrations
                     .OfType<NotificationHandlerDescriptor>()
                     .Where(static registration => !registration.IsOpenGeneric))
        {
            wrappers.TryAdd(
                registration.MessageType,
                HandlerWrapperFactory.CreateNotificationWrapper(registration.MessageType));
        }

        return wrappers;
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, RequestHandlerWrapper> CreateRequestRoutes(
        IEnumerable<Type> messageTypes,
        Dictionary<Type, RequestHandler> wrappers,
        DispatcherTelemetry? telemetry)
    {
        var routes = new Dictionary<Type, RequestHandlerWrapper>();
        foreach (var messageType in messageTypes.Where(MessageTypes.IsConcreteRequest))
        {
            var handledTypes = MessageTypes.GetAssignableTypes(messageType)
                .Where(wrappers.ContainsKey)
                .Where(handledType => wrappers[handledType].Registration.CanRoute(messageType));
            if (MostSpecificTypeSelector.Select(messageType, handledTypes) is not { } selected)
            {
                continue;
            }

            var handler = wrappers[selected];
            routes.Add(
                messageType,
                telemetry is null
                    ? handler.Wrapper
                    : TelemetryWrapperDecorator.Decorate(
                        handler.Wrapper,
                        handler.Registration,
                        messageType,
                        telemetry));
        }

        return routes;
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, NotificationHandlerWrapper> CreateNotificationRoutes(
        IEnumerable<Type> messageTypes,
        Dictionary<Type, NotificationHandlerWrapper> wrappers,
        IReadOnlyList<NotificationHandlerDescriptor> openRegistrations,
        DispatcherTelemetry? telemetry)
    {
        var routes = new Dictionary<Type, NotificationHandlerWrapper>();
        foreach (var messageType in messageTypes.Where(MessageTypes.IsConcreteNotification))
        {
            var handledType = MostSpecificTypeSelector.Select(
                messageType,
                MessageTypes.GetAssignableTypes(messageType).Where(wrappers.ContainsKey));
            var openHandlerTypes = HandlerWrapperFactory.CloseNotificationHandlers(
                messageType,
                openRegistrations);
            if (handledType is null && openHandlerTypes.Length == 0)
            {
                continue;
            }

            var wrapper = CreateNotificationRoute(messageType, handledType, openHandlerTypes, wrappers);
            routes.Add(
                messageType,
                telemetry is null
                    ? wrapper
                    : TelemetryWrapperDecorator.Decorate(wrapper, messageType, telemetry));
        }

        return routes;
    }

    /// <summary>
    /// Selects the wrapper that publishes a notification to its closed handlers, to its open generic
    /// handlers, or to both.
    /// </summary>
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static NotificationHandlerWrapper CreateNotificationRoute(
        Type messageType,
        Type? handledType,
        Type[] openHandlerTypes,
        Dictionary<Type, NotificationHandlerWrapper> wrappers)
    {
        if (handledType is null)
        {
            return HandlerWrapperFactory.CreateOpenNotificationWrapper(messageType, openHandlerTypes);
        }

        return openHandlerTypes.Length == 0
            ? wrappers[handledType]
            : HandlerWrapperFactory.CreateCompositeNotificationWrapper(
                handledType,
                messageType,
                openHandlerTypes);
    }

    private readonly record struct RequestHandler(
        RequestHandlerDescriptor Registration,
        RequestHandlerWrapper Wrapper);
}