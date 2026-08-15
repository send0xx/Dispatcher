using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Dispatcher;

internal static class DispatcherRegistryFactory
{
    [RequiresDynamicCode("Creating handler wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from registration metadata is not trimming safe.")]
    internal static DispatcherRegistry Create(
        IEnumerable<MessageRegistration> registrations,
        DispatcherTelemetry? telemetry)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var registrationList = registrations.Distinct().ToArray();
        var handlers = registrationList.OfType<HandlerRegistration>().ToArray();
        var requestWrappers = CreateRequestWrappers(handlers);
        var notificationWrappers = CreateNotificationWrappers(handlers);
        var openNotificationRegistrations = handlers
            .OfType<NotificationHandlerRegistration>()
            .Where(static registration => registration.IsOpenGeneric)
            .ToArray();
        var routeTargets = registrationList
            .Select(static registration => registration.MessageType)
            .Distinct()
            .ToArray();
        var requests = CreateRequestRoutes(routeTargets, requestWrappers, telemetry);
        var notifications = CreateNotificationRoutes(
            routeTargets,
            notificationWrappers,
            openNotificationRegistrations,
            telemetry);

        return new DispatcherRegistry(requests.ToFrozenDictionary(), notifications.ToFrozenDictionary());
    }

    [RequiresDynamicCode("Creating handler wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from registration metadata is not trimming safe.")]
    private static Dictionary<Type, (HandlerRegistration Registration, RequestHandlerWrapper Wrapper)>
        CreateRequestWrappers(IEnumerable<HandlerRegistration> registrations)
    {
        var wrappers = new Dictionary<Type, (HandlerRegistration Registration, RequestHandlerWrapper Wrapper)>();
        foreach (var registration in registrations)
        {
            RequestHandlerWrapper? wrapper = registration switch
            {
                QueryHandlerRegistration query => (RequestHandlerWrapper)CreateWrapper(
                    typeof(QueryHandlerWrapper<,>),
                    query.MessageType,
                    query.ResponseType),
                CommandWithResponseHandlerRegistration command => (RequestHandlerWrapper)CreateWrapper(
                    typeof(CommandWithResponseHandlerWrapper<,>),
                    command.MessageType,
                    command.ResponseType),
                CommandHandlerRegistration command => (RequestHandlerWrapper)CreateWrapper(
                    typeof(CommandHandlerWrapper<>),
                    command.MessageType),
                NotificationHandlerRegistration => null,
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

    [RequiresDynamicCode("Creating handler wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from registration metadata is not trimming safe.")]
    private static Dictionary<Type, NotificationHandlerWrapper> CreateNotificationWrappers(
        IEnumerable<HandlerRegistration> registrations)
    {
        var wrappers = new Dictionary<Type, NotificationHandlerWrapper>();
        foreach (var registration in registrations
                     .OfType<NotificationHandlerRegistration>()
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

    [RequiresDynamicCode("Creating telemetry wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating telemetry wrappers from registration metadata is not trimming safe.")]
    private static Dictionary<Type, RequestHandlerWrapper> CreateRequestRoutes(
        IEnumerable<Type> messageTypes,
        IReadOnlyDictionary<Type, (HandlerRegistration Registration, RequestHandlerWrapper Wrapper)> wrappers,
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
        IReadOnlyDictionary<Type, NotificationHandlerWrapper> wrappers,
        IReadOnlyList<NotificationHandlerRegistration> openRegistrations,
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
        IEnumerable<NotificationHandlerRegistration> registrations)
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

    [RequiresDynamicCode("Creating notification wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating notification wrappers from registration metadata is not trimming safe.")]
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
        HandlerRegistration registration) => registration switch
        {
            QueryHandlerRegistration query =>
                typeof(IQuery<>).MakeGenericType(query.ResponseType).IsAssignableFrom(messageType),
            CommandWithResponseHandlerRegistration command =>
                !typeof(ICommand).IsAssignableFrom(messageType) &&
                typeof(ICommand<>).MakeGenericType(command.ResponseType).IsAssignableFrom(messageType),
            CommandHandlerRegistration => typeof(ICommand).IsAssignableFrom(messageType),
            _ => false
        };

    [RequiresDynamicCode("Creating handler wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from registration metadata is not trimming safe.")]
    private static object CreateWrapper(Type wrapperType, params Type[] genericArguments) =>
        Activator.CreateInstance(wrapperType.MakeGenericType(genericArguments))!;

    private static ArgumentOutOfRangeException UnknownRegistration(HandlerRegistration registration) =>
        new(nameof(registration), registration.GetType(), "Unknown handler registration type.");
}