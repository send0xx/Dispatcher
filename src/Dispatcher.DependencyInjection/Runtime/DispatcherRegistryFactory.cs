using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
            CreateOpenHandlers(registrations),
            telemetry);

        return new DispatcherRegistry(
            requests.ToFrozenDictionary(),
            notifications.ToFrozenDictionary());
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
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

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
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

            if (HandlerTypeResolver.IsNotificationHandlerDefinition(definition))
            {
                return new HandlerRegistration(
                    HandlerKind.Notification,
                    arguments[0],
                    null,
                    handlerType,
                    null);
            }
        }

        return serviceType == handlerType && HandlerTypeResolver.IsOpenNotificationHandler(handlerType)
            ? new HandlerRegistration(
                HandlerKind.OpenNotification,
                handlerType.GetGenericArguments()[0],
                null,
                handlerType,
                null)
            : null;
    }

    private static OpenHandlerBinder[] CreateOpenHandlers(
        IEnumerable<HandlerRegistration> registrations)
    {
        var openHandlers = new List<OpenHandlerBinder>();
        foreach (var registration in registrations)
        {
            if (registration.Kind == HandlerKind.OpenNotification)
            {
                openHandlers.Add(new OpenHandlerBinder(registration.HandlerType));
            }
        }

        return openHandlers.ToArray();
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
    private static RequestHandlerWrapper CreateRequestWrapper(
        Type wrapperType,
        params Type[] genericArguments) =>
        (RequestHandlerWrapper)Activator.CreateInstance(wrapperType.MakeGenericType(genericArguments))!;

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static Dictionary<Type, RequestHandlerWrapper> CreateRequestRoutes(
        IReadOnlyList<Type> messageTypes,
        IReadOnlyDictionary<Type, RequestHandler> handlers,
        DispatcherTelemetry? telemetry)
    {
        var routes = new Dictionary<Type, RequestHandlerWrapper>();
        if (handlers.Count == 0)
        {
            return routes;
        }

        var handledTypes = new List<Type>();
        for (var index = 0; index < messageTypes.Count; index++)
        {
            var messageType = messageTypes[index];
            if (!MessageTypeResolver.IsConcreteRequest(messageType))
            {
                continue;
            }

            handledTypes.Clear();
            foreach (var assignableType in MessageTypeResolver.GetAssignableTypes(messageType))
            {
                if (handlers.TryGetValue(assignableType, out var candidate) &&
                    candidate.Registration.CanRoute(messageType))
                {
                    handledTypes.Add(assignableType);
                }
            }

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
        IReadOnlyList<Type> messageTypes,
        IReadOnlyDictionary<Type, NotificationHandlerWrapper> handlers,
        IReadOnlyList<OpenHandlerBinder> openHandlers,
        DispatcherTelemetry? telemetry)
    {
        var routes = new Dictionary<Type, NotificationHandlerWrapper>();
        if (handlers.Count == 0 && openHandlers.Count == 0)
        {
            return routes;
        }

        var handledTypes = new List<Type>();
        for (var index = 0; index < messageTypes.Count; index++)
        {
            var messageType = messageTypes[index];
            if (!MessageTypeResolver.IsConcreteNotification(messageType))
            {
                continue;
            }

            handledTypes.Clear();
            foreach (var assignableType in MessageTypeResolver.GetAssignableTypes(messageType))
            {
                if (handlers.ContainsKey(assignableType))
                {
                    handledTypes.Add(assignableType);
                }
            }

            var handledType = MessageTypeResolver.SelectMostSpecific(messageType, handledTypes);
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
        IReadOnlyList<OpenHandlerBinder> openHandlers)
    {
        if (openHandlers.Count == 0)
        {
            return [];
        }

        var handlerTypes = new List<Type>(openHandlers.Count);
        for (var index = 0; index < openHandlers.Count; index++)
        {
            if (openHandlers[index].TryClose(messageType, out var handlerType))
            {
                handlerTypes.Add(handlerType);
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
        Type HandlerType,
        Type? RouteConstraint)
    {
        internal bool IsRequest => Kind is
            HandlerKind.Query or HandlerKind.CommandWithResponse or HandlerKind.Command;

        internal bool CanRoute(Type messageType) => Kind switch
        {
            HandlerKind.Query => RouteConstraint!.IsAssignableFrom(messageType),
            HandlerKind.CommandWithResponse =>
                !typeof(ICommand).IsAssignableFrom(messageType) &&
                RouteConstraint!.IsAssignableFrom(messageType),
            HandlerKind.Command => typeof(ICommand).IsAssignableFrom(messageType),
            _ => false
        };
    }

    private readonly struct OpenHandlerBinder
    {
        private readonly Type _handlerType;
        private readonly Type[] _constraints;
        private readonly GenericParameterAttributes _specialConstraints;

        internal OpenHandlerBinder(Type handlerType)
        {
            var parameter = handlerType.GetGenericArguments()[0];
            _handlerType = handlerType;
            _constraints = parameter.GetGenericParameterConstraints();
            _specialConstraints = parameter.GenericParameterAttributes &
                                  GenericParameterAttributes.SpecialConstraintMask;
        }

        [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
        internal bool TryClose(Type messageType, [NotNullWhen(true)] out Type? closedHandlerType)
        {
            if (!SatisfiesConstraints(messageType))
            {
                closedHandlerType = null;
                return false;
            }

            try
            {
                closedHandlerType = _handlerType.MakeGenericType(messageType);
                return true;
            }
            catch (ArgumentException)
            {
                // Let the runtime decide constraints that refer to the generic parameter itself.
                closedHandlerType = null;
                return false;
            }
        }

        private bool SatisfiesConstraints(Type messageType)
        {
            if ((_specialConstraints & GenericParameterAttributes.ReferenceTypeConstraint) != 0 &&
                messageType.IsValueType)
            {
                return false;
            }

            if ((_specialConstraints & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0 &&
                (!messageType.IsValueType || Nullable.GetUnderlyingType(messageType) is not null))
            {
                return false;
            }

            if ((_specialConstraints & GenericParameterAttributes.DefaultConstructorConstraint) != 0 &&
                !messageType.IsValueType &&
                messageType.GetConstructor(Type.EmptyTypes) is null)
            {
                return false;
            }

            foreach (var constraint in _constraints)
            {
                if (!constraint.ContainsGenericParameters && !constraint.IsAssignableFrom(messageType))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private readonly record struct RequestHandler(
        HandlerRegistration Registration,
        RequestHandlerWrapper Wrapper);
}