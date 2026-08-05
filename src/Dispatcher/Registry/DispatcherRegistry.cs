using System.Collections.Frozen;

namespace Dispatcher;

/// <summary>
/// Provides immutable handler lookup tables used during dispatch.
/// </summary>
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

    /// <summary>
    /// Creates a registry from handler registrations.
    /// </summary>
    /// <param name="registrations">The handler registrations to include.</param>
    /// <returns>An immutable dispatcher registry.</returns>
    /// <exception cref="DuplicateHandlerException">A query or command has multiple handlers.</exception>
    public static DispatcherRegistry Create(IEnumerable<HandlerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

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

            var wrapper = CreateRequestWrapper(registration);
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

    private static RequestHandlerWrapper CreateRequestWrapper(HandlerRegistration registration)
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

        return (RequestHandlerWrapper)Activator.CreateInstance(wrapperType)!;
    }

    private static NotificationHandlerWrapper CreateNotificationWrapper(Type notificationType)
    {
        var wrapperType = typeof(NotificationHandlerWrapper<>).MakeGenericType(notificationType);
        return (NotificationHandlerWrapper)Activator.CreateInstance(wrapperType)!;
    }

    private static InvalidOperationException MissingResponseType(HandlerRegistration registration) =>
        new($"Registration for '{registration.MessageType.FullName}' requires a response type.");
}