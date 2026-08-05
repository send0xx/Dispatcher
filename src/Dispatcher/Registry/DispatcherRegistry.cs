using System.Collections.Frozen;

namespace Dispatcher;

public sealed class DispatcherRegistry
{
    internal FrozenDictionary<Type, RequestHandlerWrapper> RequestHandlers { get; }
    internal FrozenDictionary<Type, NotificationHandlerWrapper> NotificationHandlers { get; }

    private DispatcherRegistry(
        FrozenDictionary<Type, RequestHandlerWrapper> requestHandlers,
        FrozenDictionary<Type, NotificationHandlerWrapper> notificationHandlers)
    {
        RequestHandlers = requestHandlers;
        NotificationHandlers = notificationHandlers;
    }

    public static DispatcherRegistry Create(IEnumerable<HandlerRegistration> registrations)
        => Create(registrations, null);

    public static DispatcherRegistry Create(
        IEnumerable<HandlerRegistration> registrations,
        IEnumerable<PipelineBehaviorRegistration>? pipelineBehaviorRegistrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var behaviorRegistrations = pipelineBehaviorRegistrations?.ToArray();

        var requests = new Dictionary<Type, (RequestHandlerWrapper Wrapper, Type HandlerType)>();
        var notifications = new Dictionary<Type, NotificationHandlerWrapper>();

        foreach (var registration in registrations)
        {
            ArgumentNullException.ThrowIfNull(registration);

            if (registration.Kind == HandlerKind.Notification)
            {
                notifications.TryAdd(
                    registration.MessageType,
                    CreateNotificationWrapper(registration.MessageType));
                continue;
            }

            var wrapper = CreateRequestWrapper(registration, GetPipelineMode(registration, behaviorRegistrations));
            if (!requests.TryAdd(registration.MessageType, (wrapper, registration.HandlerType)))
            {
                var existing = requests[registration.MessageType];
                throw new DuplicateHandlerException(
                    registration.MessageType,
                    existing.HandlerType,
                    registration.HandlerType);
            }
        }

        return new DispatcherRegistry(
            requests.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.Wrapper),
            notifications.ToFrozenDictionary());
    }

    private static RequestHandlerWrapper CreateRequestWrapper(
        HandlerRegistration registration,
        PipelineMode pipelineMode)
    {
        var wrapperType = registration.Kind switch
        {
            HandlerKind.Query => typeof(QueryHandlerWrapper<,>).MakeGenericType(
                registration.MessageType,
                registration.ResponseType ?? throw MissingResponseType(registration)),
            HandlerKind.CommandWithResponse => typeof(CommandWithResponseHandlerWrapper<,>).MakeGenericType(
                registration.MessageType,
                registration.ResponseType ?? throw MissingResponseType(registration)),
            HandlerKind.Command => typeof(CommandHandlerWrapper<>).MakeGenericType(registration.MessageType),
            _ => throw new ArgumentOutOfRangeException(nameof(registration), registration.Kind, "Unknown handler kind.")
        };

        return (RequestHandlerWrapper)Activator.CreateInstance(wrapperType, pipelineMode)!;
    }

    private static PipelineMode GetPipelineMode(
        HandlerRegistration registration,
        PipelineBehaviorRegistration[]? behaviorRegistrations)
    {
        if (behaviorRegistrations is null)
        {
            return PipelineMode.Dynamic;
        }

        var responseType = registration.ResponseType ?? typeof(Unit);
        var pipelineType = typeof(IPipelineBehavior<,>).MakeGenericType(registration.MessageType, responseType);
        var applicable = behaviorRegistrations.Where(item =>
            item.ServiceType == pipelineType ||
            item.ServiceType.IsGenericTypeDefinition &&
            item.ServiceType == typeof(IPipelineBehavior<,>)).ToArray();

        if (applicable.Length == 0)
        {
            return PipelineMode.None;
        }

        return applicable.All(static item => item.IsReusable)
            ? PipelineMode.Reusable
            : PipelineMode.Dynamic;
    }

    private static NotificationHandlerWrapper CreateNotificationWrapper(Type notificationType)
    {
        var wrapperType = typeof(NotificationHandlerWrapper<>).MakeGenericType(notificationType);
        return (NotificationHandlerWrapper)Activator.CreateInstance(wrapperType)!;
    }

    private static InvalidOperationException MissingResponseType(HandlerRegistration registration) =>
        new($"Registration for '{registration.MessageType.FullName}' requires a response type.");
}