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
            .Concat(registrations.Handlers.Select(static registration => registration.MessageType))
            .Distinct()
            .ToArray();

        return new DispatcherRegistry(
            CreateRequestRoutes(messageTypes, registrations.Handlers, telemetry).ToFrozenDictionary(),
            CreateNotificationRoutes(messageTypes, registrations, telemetry).ToFrozenDictionary());
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    private static HandlerRegistrations ReadRegistrations(IEnumerable<ServiceDescriptor> services)
    {
        var handlers = new List<HandlerRegistration>();
        var openHandlers = new List<Type>();
        var mappedOpenHandlers = new List<Type>();

        foreach (var descriptor in services)
        {
            if (descriptor.IsKeyedService)
            {
                continue;
            }

            var serviceType = descriptor.ServiceType;
            if (serviceType == typeof(INotificationHandler<>) &&
                descriptor.ImplementationType is { } mappedHandlerType &&
                HandlerTypeResolver.IsOpenNotificationHandler(mappedHandlerType))
            {
                mappedOpenHandlers.Add(mappedHandlerType);
                continue;
            }

            var handlerType = descriptor.ImplementationType ??
                              descriptor.ImplementationInstance?.GetType() ??
                              serviceType;
            if (serviceType == handlerType && HandlerTypeResolver.IsOpenNotificationHandler(handlerType))
            {
                openHandlers.Add(handlerType);
                continue;
            }

            if (TryReadHandler(serviceType, handlerType) is { } registration)
            {
                handlers.Add(registration);
            }
        }

        if (mappedOpenHandlers.Count > 0)
        {
            var mappedHandlerTypes = mappedOpenHandlers.ToHashSet();
            openHandlers.RemoveAll(mappedHandlerTypes.Contains);
        }

        return new HandlerRegistrations(
            handlers.ToArray(),
            openHandlers.ToArray(),
            mappedOpenHandlers.ToArray());
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    private static HandlerRegistration? TryReadHandler(Type serviceType, Type handlerType)
    {
        if (!serviceType.IsGenericType || serviceType.ContainsGenericParameters)
        {
            return null;
        }

        var definition = serviceType.GetGenericTypeDefinition();
        var arguments = serviceType.GetGenericArguments();
        if (HandlerTypeResolver.IsQueryHandlerDefinition(definition))
        {
            return new HandlerRegistration(
                HandlerKind.Query,
                arguments[0],
                arguments[1],
                handlerType,
                typeof(IQuery<>).MakeGenericType(arguments[1]));
        }

        if (HandlerTypeResolver.IsCommandWithResponseHandlerDefinition(definition))
        {
            return new HandlerRegistration(
                HandlerKind.CommandWithResponse,
                arguments[0],
                arguments[1],
                handlerType,
                typeof(ICommand<>).MakeGenericType(arguments[1]));
        }

        if (HandlerTypeResolver.IsCommandHandlerDefinition(definition))
        {
            return new HandlerRegistration(
                HandlerKind.Command,
                arguments[0],
                null,
                handlerType,
                null);
        }

        return HandlerTypeResolver.IsNotificationHandlerDefinition(definition)
            ? new HandlerRegistration(
                HandlerKind.Notification,
                arguments[0],
                null,
                handlerType,
                null)
            : null;
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, RequestHandlerWrapper> CreateRequestRoutes(
        Type[] messageTypes,
        HandlerRegistration[] registrations,
        DispatcherTelemetry? telemetry)
    {
        var handlers = CreateRequestHandlers(registrations);
        var routes = new Dictionary<Type, RequestHandlerWrapper>();
        if (handlers.Count == 0)
        {
            return routes;
        }

        var handledTypes = new List<Type>();
        for (var index = 0; index < messageTypes.Length; index++)
        {
            var messageType = messageTypes[index];
            if (!MessageTypeResolver.IsConcreteRequest(messageType))
            {
                continue;
            }

            handledTypes.Clear();
            foreach (var handledType in MessageTypeResolver.GetAssignableTypes(messageType))
            {
                if (handlers.TryGetValue(handledType, out var handler) &&
                    handler.Registration.CanRoute(messageType))
                {
                    handledTypes.Add(handledType);
                }
            }

            if (MessageTypeResolver.SelectMostSpecific(messageType, handledTypes) is not { } selectedType)
            {
                continue;
            }

            var selectedHandler = handlers[selectedType];
            routes.Add(
                messageType,
                telemetry is null
                    ? selectedHandler.Wrapper
                    : DecorateRequest(selectedHandler, messageType, telemetry));
        }

        return routes;
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, RequestHandler> CreateRequestHandlers(
        HandlerRegistration[] registrations)
    {
        var handlers = new Dictionary<Type, RequestHandler>();
        for (var index = 0; index < registrations.Length; index++)
        {
            var registration = registrations[index];
            if (!registration.IsRequest)
            {
                continue;
            }

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
            HandlerKind.Query => CreateWrapper<RequestHandlerWrapper>(
                typeof(QueryHandlerWrapper<,>),
                [registration.MessageType, registration.ResponseType!]),
            HandlerKind.CommandWithResponse => CreateWrapper<RequestHandlerWrapper>(
                typeof(CommandWithResponseHandlerWrapper<,>),
                [registration.MessageType, registration.ResponseType!]),
            HandlerKind.Command => CreateWrapper<RequestHandlerWrapper>(
                typeof(CommandHandlerWrapper<>),
                [registration.MessageType]),
            _ => throw new ArgumentOutOfRangeException(
                nameof(registration),
                registration.Kind,
                "Unsupported request handler kind.")
        };

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
    private static Dictionary<Type, NotificationHandlerWrapper> CreateNotificationRoutes(
        Type[] messageTypes,
        HandlerRegistrations registrations,
        DispatcherTelemetry? telemetry)
    {
        var handlers = CreateNotificationHandlers(registrations.Handlers);
        var routes = new Dictionary<Type, NotificationHandlerWrapper>();
        if (handlers.Count == 0 &&
            registrations.OpenHandlers.Length == 0 &&
            registrations.MappedOpenHandlers.Length == 0)
        {
            return routes;
        }

        var handledTypes = new List<Type>();
        for (var index = 0; index < messageTypes.Length; index++)
        {
            var messageType = messageTypes[index];
            if (!MessageTypeResolver.IsConcreteNotification(messageType))
            {
                continue;
            }

            handledTypes.Clear();
            foreach (var handledType in MessageTypeResolver.GetAssignableTypes(messageType))
            {
                if (handlers.ContainsKey(handledType))
                {
                    handledTypes.Add(handledType);
                }
            }

            var selectedType = MessageTypeResolver.SelectMostSpecific(messageType, handledTypes);
            var openHandlerTypes = CloseOpenHandlers(registrations.OpenHandlers, messageType);
            if (selectedType is null && CanCloseAny(registrations.MappedOpenHandlers, messageType))
            {
                // Mapped handlers are resolved by NotificationHandlerWrapper<TNotification>.
                selectedType = messageType;
            }

            if (selectedType is null && openHandlerTypes.Length == 0)
            {
                continue;
            }

            var wrapper = CreateNotificationWrapper(
                messageType,
                selectedType,
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
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, NotificationHandlerWrapper> CreateNotificationHandlers(
        HandlerRegistration[] registrations)
    {
        var handlers = new Dictionary<Type, NotificationHandlerWrapper>();
        for (var index = 0; index < registrations.Length; index++)
        {
            var registration = registrations[index];
            if (registration.Kind != HandlerKind.Notification)
            {
                continue;
            }

            handlers.TryAdd(
                registration.MessageType,
                CreateWrapper<NotificationHandlerWrapper>(
                    typeof(NotificationHandlerWrapper<>),
                    [registration.MessageType]));
        }

        return handlers;
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    private static Type[] CloseOpenHandlers(Type[] handlerTypes, Type messageType)
    {
        if (handlerTypes.Length == 0)
        {
            return [];
        }

        var closedHandlerTypes = new List<Type>(handlerTypes.Length);
        for (var index = 0; index < handlerTypes.Length; index++)
        {
            if (TryClose(handlerTypes[index], messageType, out var closedHandlerType))
            {
                closedHandlerTypes.Add(closedHandlerType);
            }
        }

        return closedHandlerTypes.ToArray();
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    private static bool CanCloseAny(Type[] handlerTypes, Type messageType)
    {
        for (var index = 0; index < handlerTypes.Length; index++)
        {
            if (TryClose(handlerTypes[index], messageType, out _))
            {
                return true;
            }
        }

        return false;
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    private static bool TryClose(
        Type handlerType,
        Type messageType,
        [NotNullWhen(true)] out Type? closedHandlerType)
    {
        try
        {
            closedHandlerType = handlerType.MakeGenericType(messageType);
            return true;
        }
        catch (ArgumentException)
        {
            closedHandlerType = null;
            return false;
        }
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static NotificationHandlerWrapper CreateNotificationWrapper(
        Type messageType,
        Type? selectedType,
        Type[] openHandlerTypes,
        Dictionary<Type, NotificationHandlerWrapper> handlers)
    {
        if (selectedType is null)
        {
            return CreateWrapper<NotificationHandlerWrapper>(
                typeof(OpenNotificationHandlerWrapper<>),
                [messageType],
                [openHandlerTypes]);
        }

        if (openHandlerTypes.Length > 0)
        {
            return CreateWrapper<NotificationHandlerWrapper>(
                typeof(CompositeNotificationHandlerWrapper<,>),
                [selectedType, messageType],
                [openHandlerTypes]);
        }

        return handlers.TryGetValue(selectedType, out var handler)
            ? handler
            : CreateWrapper<NotificationHandlerWrapper>(
                typeof(NotificationHandlerWrapper<>),
                [selectedType]);
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static TWrapper CreateWrapper<TWrapper>(
        Type wrapperType,
        Type[] genericArguments,
        object?[]? constructorArguments = null)
        where TWrapper : class =>
        (TWrapper)Activator.CreateInstance(
            wrapperType.MakeGenericType(genericArguments),
            constructorArguments)!;

    private enum HandlerKind
    {
        Query,
        CommandWithResponse,
        Command,
        Notification
    }

    private readonly record struct HandlerRegistrations(
        HandlerRegistration[] Handlers,
        Type[] OpenHandlers,
        Type[] MappedOpenHandlers);

    private readonly record struct HandlerRegistration(
        HandlerKind Kind,
        Type MessageType,
        Type? ResponseType,
        Type HandlerType,
        Type? RouteContract)
    {
        internal bool IsRequest =>
            Kind is HandlerKind.Query or HandlerKind.CommandWithResponse or HandlerKind.Command;

        internal bool CanRoute(Type messageType) => Kind switch
        {
            HandlerKind.Query => RouteContract!.IsAssignableFrom(messageType),
            HandlerKind.CommandWithResponse =>
                !typeof(ICommand).IsAssignableFrom(messageType) &&
                RouteContract!.IsAssignableFrom(messageType),
            HandlerKind.Command => typeof(ICommand).IsAssignableFrom(messageType),
            _ => false
        };
    }

    private readonly record struct RequestHandler(
        HandlerRegistration Registration,
        RequestHandlerWrapper Wrapper);
}