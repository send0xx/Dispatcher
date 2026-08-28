using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Dispatcher;

internal static class DispatcherRegistryFactory
{
    [RequiresDynamicCode("Creating handler wrappers from service descriptors requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from service descriptors is not trimming safe.")]
    internal static DispatcherRegistry Create(
        IEnumerable<HandlerDescriptor> handlers,
        IEnumerable<Type> routeTargets,
        DispatcherTelemetry? telemetry)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(routeTargets);

        var registrations = handlers.ToArray();
        var requestWrappers = CreateRequestWrappers(registrations);
        var notificationWrappers = CreateNotificationWrappers(registrations);
        var openNotificationRegistrations = registrations
            .OfType<NotificationHandlerDescriptor>()
            .Where(static registration => registration.IsOpenGeneric)
            .ToArray();
        var messageTypes = routeTargets
            .Concat(registrations.Select(static registration => registration.MessageType))
            .Distinct()
            .ToArray();
        var requests = CreateRequestRoutes(messageTypes, requestWrappers, telemetry);
        var notifications = CreateNotificationRoutes(
            messageTypes,
            notificationWrappers,
            openNotificationRegistrations,
            telemetry);

        return new DispatcherRegistry(requests.ToFrozenDictionary(), notifications.ToFrozenDictionary());
    }

    [RequiresDynamicCode("Creating handler wrappers from service descriptors requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from service descriptors is not trimming safe.")]
    private static Dictionary<Type, (HandlerDescriptor Registration, RequestHandlerWrapper Wrapper)>
        CreateRequestWrappers(IEnumerable<HandlerDescriptor> registrations)
    {
        var wrappers = new Dictionary<Type, (HandlerDescriptor Registration, RequestHandlerWrapper Wrapper)>();
        foreach (var registration in registrations)
        {
            var wrapper = registration switch
            {
                QueryHandlerDescriptor query => (RequestHandlerWrapper)CreateWrapper(
                    typeof(QueryHandlerWrapper<,>),
                    query.MessageType,
                    query.ResponseType),
                CommandWithResponseHandlerDescriptor command => (RequestHandlerWrapper)CreateWrapper(
                    typeof(CommandWithResponseHandlerWrapper<,>),
                    command.MessageType,
                    command.ResponseType),
                CommandHandlerDescriptor command => (RequestHandlerWrapper)CreateWrapper(
                    typeof(CommandHandlerWrapper<>),
                    command.MessageType),
                NotificationHandlerDescriptor => null,
                _ => throw UnknownRegistration(registration)
            };
            if (wrapper is null)
            {
                continue;
            }

            if (!wrappers.TryAdd(registration.MessageType, (registration, wrapper)))
            {
                var existing = wrappers[registration.MessageType].Registration;
                throw new DuplicateHandlerException(
                    registration.MessageType,
                    existing.HandlerType,
                    registration.HandlerType);
            }
        }

        return wrappers;
    }

    [RequiresDynamicCode("Creating handler wrappers from service descriptors requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from service descriptors is not trimming safe.")]
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
                (NotificationHandlerWrapper)CreateWrapper(
                    typeof(NotificationHandlerWrapper<>),
                    registration.MessageType));
        }

        return wrappers;
    }

    [RequiresDynamicCode("Creating telemetry wrappers from service descriptors requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating telemetry wrappers from service descriptors is not trimming safe.")]
    private static Dictionary<Type, RequestHandlerWrapper> CreateRequestRoutes(
        IEnumerable<Type> messageTypes,
        Dictionary<Type, (HandlerDescriptor Registration, RequestHandlerWrapper Wrapper)> wrappers,
        DispatcherTelemetry? telemetry)
    {
        var routes = new Dictionary<Type, RequestHandlerWrapper>();
        foreach (var messageType in messageTypes.Where(IsConcreteRequest))
        {
            var candidates = GetAssignableTypes(messageType)
                .Where(wrappers.ContainsKey)
                .Where(candidate => IsCompatibleRequestRoute(
                    messageType,
                    wrappers[candidate].Registration));
            var handledType = MostSpecificTypeSelector.Select(messageType, candidates);
            if (handledType is null)
            {
                continue;
            }

            var prepared = wrappers[handledType];
            routes.Add(
                messageType,
                telemetry is null
                    ? prepared.Wrapper
                    : TelemetryWrapperDecorator.Decorate(
                        prepared.Wrapper,
                        prepared.Registration,
                        messageType,
                        telemetry));
        }

        return routes;
    }

    private static Dictionary<Type, NotificationHandlerWrapper> CreateNotificationRoutes(
        IEnumerable<Type> messageTypes,
        Dictionary<Type, NotificationHandlerWrapper> wrappers,
        IReadOnlyList<NotificationHandlerDescriptor> openRegistrations,
        DispatcherTelemetry? telemetry)
    {
        var routes = new Dictionary<Type, NotificationHandlerWrapper>();
        foreach (var messageType in messageTypes.Where(IsConcreteNotification))
        {
            var candidates = GetAssignableTypes(messageType).Where(wrappers.ContainsKey);
            var handledType = MostSpecificTypeSelector.Select(messageType, candidates);
            var openHandlerTypes = CloseCompatibleHandlers(messageType, openRegistrations);
            if (handledType is null && openHandlerTypes.Length == 0)
            {
                continue;
            }

            var wrapper = handledType is null
                ? CreateNotificationWrapper(
                    typeof(OpenNotificationHandlerWrapper<>),
                    openHandlerTypes,
                    messageType)
                : openHandlerTypes.Length == 0
                    ? wrappers[handledType]
                    : CreateNotificationWrapper(
                        typeof(CompositeNotificationHandlerWrapper<,>),
                        openHandlerTypes,
                        handledType,
                        messageType);
            routes.Add(
                messageType,
                telemetry is null
                    ? wrapper
                    : TelemetryWrapperDecorator.Decorate(wrapper, messageType, telemetry));
        }

        return routes;
    }

    private static Type[] CloseCompatibleHandlers(
        Type messageType,
        IEnumerable<NotificationHandlerDescriptor> registrations)
    {
        var handlerTypes = new List<Type>();
        foreach (var registration in registrations)
        {
            try
            {
                handlerTypes.Add(registration.HandlerType.MakeGenericType(messageType));
            }
            catch (ArgumentException)
            {
                // The concrete notification does not satisfy the handler's generic constraints.
            }
        }

        return handlerTypes.ToArray();
    }

    [RequiresDynamicCode("Creating notification wrappers from service descriptors requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating notification wrappers from service descriptors is not trimming safe.")]
    private static NotificationHandlerWrapper CreateNotificationWrapper(
        Type wrapperType,
        Type[] handlerTypes,
        params Type[] genericArguments) =>
        (NotificationHandlerWrapper)Activator.CreateInstance(
            wrapperType.MakeGenericType(genericArguments),
            [handlerTypes])!;

    private static bool IsConcreteRequest(Type type) =>
        IsConcreteMessage(type) && typeof(IRequest).IsAssignableFrom(type);

    private static bool IsConcreteNotification(Type type) =>
        IsConcreteMessage(type) && typeof(INotification).IsAssignableFrom(type);

    private static bool IsConcreteMessage(Type type) =>
        type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false };

    private static IEnumerable<Type> GetAssignableTypes(Type messageType)
    {
        for (var current = messageType; current is not null; current = current.BaseType)
        {
            yield return current;
        }

        foreach (var @interface in messageType.GetInterfaces())
        {
            yield return @interface;
        }
    }

    private static bool IsCompatibleRequestRoute(
        Type messageType,
        HandlerDescriptor registration) => registration switch
        {
            QueryHandlerDescriptor query =>
                typeof(IQuery<>).MakeGenericType(query.ResponseType).IsAssignableFrom(messageType),
            CommandWithResponseHandlerDescriptor command =>
                !typeof(ICommand).IsAssignableFrom(messageType) &&
                typeof(ICommand<>).MakeGenericType(command.ResponseType).IsAssignableFrom(messageType),
            CommandHandlerDescriptor => typeof(ICommand).IsAssignableFrom(messageType),
            _ => false
        };

    [RequiresDynamicCode("Creating handler wrappers from service descriptors requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from service descriptors is not trimming safe.")]
    private static object CreateWrapper(Type wrapperType, params Type[] genericArguments) =>
        Activator.CreateInstance(wrapperType.MakeGenericType(genericArguments))!;

    private static ArgumentOutOfRangeException UnknownRegistration(HandlerDescriptor registration) =>
        new(nameof(registration), registration.GetType(), "Unknown handler registration type.");
}