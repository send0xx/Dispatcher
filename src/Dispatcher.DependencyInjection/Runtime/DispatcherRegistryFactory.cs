using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Dispatcher.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher;

internal static class DispatcherRegistryFactory
{
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal static DispatcherRegistry Create(
        IEnumerable<ServiceDescriptor> services,
        IEnumerable<Type> routeTargets,
        DispatcherTelemetry? telemetry)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(routeTargets);

        var registrations = ReadRegistrations(services);
        var messageTypes = routeTargets
            .Concat(registrations
                .Where(static registration => registration.Kind != HandlerKind.OpenNotification)
                .Select(static registration => registration.MessageType))
            .Distinct()
            .ToArray();
        var requests = CreateRequestRoutes(
            messageTypes,
            CreateRequestHandlers(registrations),
            telemetry);
        var notifications = CreateNotificationRoutes(
            messageTypes,
            CreateNotificationHandlers(registrations),
            registrations
                .Where(static registration => registration.Kind == HandlerKind.OpenNotification)
                .ToArray(),
            telemetry);

        return new DispatcherRegistry(
            requests.ToFrozenDictionary(),
            notifications.ToFrozenDictionary());
    }

    private static HandlerRegistration[] ReadRegistrations(IEnumerable<ServiceDescriptor> services)
    {
        var registrations = new List<HandlerRegistration>();
        foreach (var descriptor in services)
        {
            if (TryReadRegistration(descriptor) is { } registration)
            {
                registrations.Add(registration);
            }
        }

        return registrations.ToArray();
    }

    private static HandlerRegistration? TryReadRegistration(ServiceDescriptor descriptor)
    {
        if (descriptor.IsKeyedService)
        {
            return null;
        }

        var serviceType = descriptor.ServiceType;
        var handlerType = descriptor.ImplementationType ??
                          descriptor.ImplementationInstance?.GetType() ??
                          serviceType;
        if (serviceType.IsGenericType)
        {
            var definition = serviceType.GetGenericTypeDefinition();
            var arguments = serviceType.GetGenericArguments();
            if (definition == typeof(IQueryHandler<,>))
            {
                return new HandlerRegistration(
                    HandlerKind.Query,
                    arguments[0],
                    arguments[1],
                    handlerType);
            }

            if (definition == typeof(ICommandHandler<,>))
            {
                return new HandlerRegistration(
                    HandlerKind.CommandWithResponse,
                    arguments[0],
                    arguments[1],
                    handlerType);
            }

            if (definition == typeof(ICommandHandler<>))
            {
                return new HandlerRegistration(
                    HandlerKind.Command,
                    arguments[0],
                    null,
                    handlerType);
            }

            if (definition == typeof(INotificationHandler<>))
            {
                return new HandlerRegistration(
                    HandlerKind.Notification,
                    arguments[0],
                    null,
                    handlerType);
            }
        }

        return serviceType == handlerType && IsOpenNotificationHandler(handlerType)
            ? new HandlerRegistration(
                HandlerKind.OpenNotification,
                handlerType.GetGenericArguments()[0],
                null,
                handlerType)
            : null;
    }

    private static bool IsOpenNotificationHandler(Type handlerType)
    {
        if (!handlerType.IsGenericTypeDefinition ||
            handlerType.GetGenericArguments() is not [var parameter])
        {
            return false;
        }

        return handlerType.GetInterfaces().Any(handlerInterface =>
            handlerInterface.IsGenericType &&
            handlerInterface.GetGenericTypeDefinition() == typeof(INotificationHandler<>) &&
            handlerInterface.GetGenericArguments()[0] == parameter);
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, RequestHandler> CreateRequestHandlers(
        IEnumerable<HandlerRegistration> registrations)
    {
        var handlers = new Dictionary<Type, RequestHandler>();
        foreach (var registration in registrations.Where(static registration => registration.IsRequest))
        {
            if (handlers.TryGetValue(registration.MessageType, out var existing))
            {
                throw new DuplicateHandlerException(
                    registration.MessageType,
                    existing.Registration.HandlerType,
                    registration.HandlerType);
            }

            handlers.Add(
                registration.MessageType,
                new RequestHandler(registration, CreateRequestWrapper(registration)));
        }

        return handlers;
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static RequestHandlerWrapper CreateRequestWrapper(HandlerRegistration registration) =>
        registration.Kind switch
        {
            HandlerKind.Query => CreateRequestWrapper(
                typeof(QueryHandlerWrapper<,>),
                registration.MessageType,
                registration.ResponseType!),
            HandlerKind.CommandWithResponse => CreateRequestWrapper(
                typeof(CommandWithResponseHandlerWrapper<,>),
                registration.MessageType,
                registration.ResponseType!),
            HandlerKind.Command => CreateRequestWrapper(
                typeof(CommandHandlerWrapper<>),
                registration.MessageType),
            _ => throw new ArgumentOutOfRangeException(
                nameof(registration),
                registration.Kind,
                "Unsupported request handler kind.")
        };

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, RequestHandlerWrapper> CreateRequestRoutes(
        IEnumerable<Type> messageTypes,
        IReadOnlyDictionary<Type, RequestHandler> handlers,
        DispatcherTelemetry? telemetry)
    {
        var routes = new Dictionary<Type, RequestHandlerWrapper>();
        foreach (var messageType in messageTypes.Where(MessageTypeResolver.IsConcreteRequest))
        {
            var handledTypes = MessageTypeResolver.GetAssignableTypes(messageType)
                .Where(handlers.ContainsKey)
                .Where(type => handlers[type].Registration.CanRoute(messageType));
            if (MessageTypeResolver.SelectMostSpecific(messageType, handledTypes) is not { } handledType)
            {
                continue;
            }

            var handler = handlers[handledType];
            routes.Add(
                messageType,
                telemetry is null
                    ? handler.Wrapper
                    : DecorateRequest(handler, messageType, telemetry));
        }

        return routes;
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static RequestHandlerWrapper DecorateRequest(
        RequestHandler handler,
        Type messageType,
        DispatcherTelemetry telemetry) =>
        handler.Registration.Kind switch
        {
            HandlerKind.Query => TelemetryWrapperDecorator.DecorateQuery(
                handler.Wrapper,
                handler.Registration.ResponseType!,
                messageType,
                telemetry),
            HandlerKind.CommandWithResponse => TelemetryWrapperDecorator.DecorateCommandWithResponse(
                handler.Wrapper,
                handler.Registration.ResponseType!,
                messageType,
                telemetry),
            HandlerKind.Command => TelemetryWrapperDecorator.DecorateCommand(
                handler.Wrapper,
                messageType,
                telemetry),
            _ => throw new ArgumentOutOfRangeException(
                nameof(handler),
                handler.Registration.Kind,
                "Unsupported request handler kind.")
        };

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, NotificationHandlerWrapper> CreateNotificationHandlers(
        IEnumerable<HandlerRegistration> registrations)
    {
        var handlers = new Dictionary<Type, NotificationHandlerWrapper>();
        foreach (var registration in registrations
                     .Where(static registration => registration.Kind == HandlerKind.Notification))
        {
            handlers.TryAdd(
                registration.MessageType,
                CreateNotificationWrapper(
                    typeof(NotificationHandlerWrapper<>),
                    registration.MessageType));
        }

        return handlers;
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, NotificationHandlerWrapper> CreateNotificationRoutes(
        IEnumerable<Type> messageTypes,
        IReadOnlyDictionary<Type, NotificationHandlerWrapper> handlers,
        IReadOnlyList<HandlerRegistration> openHandlers,
        DispatcherTelemetry? telemetry)
    {
        var routes = new Dictionary<Type, NotificationHandlerWrapper>();
        foreach (var messageType in messageTypes.Where(MessageTypeResolver.IsConcreteNotification))
        {
            var handledType = MessageTypeResolver.SelectMostSpecific(
                messageType,
                MessageTypeResolver.GetAssignableTypes(messageType).Where(handlers.ContainsKey));
            var openHandlerTypes = CloseOpenHandlers(messageType, openHandlers);
            if (handledType is null && openHandlerTypes.Length == 0)
            {
                continue;
            }

            var wrapper = CreateNotificationRoute(
                messageType,
                handledType,
                openHandlerTypes,
                handlers);
            routes.Add(
                messageType,
                telemetry is null
                    ? wrapper
                    : TelemetryWrapperDecorator.Decorate(wrapper, messageType, telemetry));
        }

        return routes;
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    private static Type[] CloseOpenHandlers(
        Type messageType,
        IEnumerable<HandlerRegistration> registrations)
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

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static NotificationHandlerWrapper CreateNotificationRoute(
        Type messageType,
        Type? handledType,
        Type[] openHandlerTypes,
        IReadOnlyDictionary<Type, NotificationHandlerWrapper> handlers)
    {
        if (handledType is null)
        {
            return CreateNotificationWrapper(
                typeof(OpenNotificationHandlerWrapper<>),
                [messageType],
                openHandlerTypes);
        }

        return openHandlerTypes.Length == 0
            ? handlers[handledType]
            : CreateNotificationWrapper(
                typeof(CompositeNotificationHandlerWrapper<,>),
                [handledType, messageType],
                openHandlerTypes);
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static RequestHandlerWrapper CreateRequestWrapper(
        Type wrapperType,
        params Type[] genericArguments) =>
        (RequestHandlerWrapper)Activator.CreateInstance(wrapperType.MakeGenericType(genericArguments))!;

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static NotificationHandlerWrapper CreateNotificationWrapper(
        Type wrapperType,
        params Type[] genericArguments) =>
        (NotificationHandlerWrapper)Activator.CreateInstance(wrapperType.MakeGenericType(genericArguments))!;

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static NotificationHandlerWrapper CreateNotificationWrapper(
        Type wrapperType,
        Type[] genericArguments,
        Type[] handlerTypes) =>
        (NotificationHandlerWrapper)Activator.CreateInstance(
            wrapperType.MakeGenericType(genericArguments),
            [handlerTypes])!;

    private enum HandlerKind
    {
        Query,
        CommandWithResponse,
        Command,
        Notification,
        OpenNotification
    }

    private readonly record struct HandlerRegistration(
        HandlerKind Kind,
        Type MessageType,
        Type? ResponseType,
        Type HandlerType)
    {
        internal bool IsRequest => Kind is
            HandlerKind.Query or HandlerKind.CommandWithResponse or HandlerKind.Command;

        [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
        internal bool CanRoute(Type messageType) => Kind switch
        {
            HandlerKind.Query => typeof(IQuery<>)
                .MakeGenericType(ResponseType!)
                .IsAssignableFrom(messageType),
            HandlerKind.CommandWithResponse =>
                !typeof(ICommand).IsAssignableFrom(messageType) &&
                typeof(ICommand<>)
                    .MakeGenericType(ResponseType!)
                    .IsAssignableFrom(messageType),
            HandlerKind.Command => typeof(ICommand).IsAssignableFrom(messageType),
            _ => false
        };
    }

    private readonly record struct RequestHandler(
        HandlerRegistration Registration,
        RequestHandlerWrapper Wrapper);
}