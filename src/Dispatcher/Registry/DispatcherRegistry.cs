using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Dispatcher;

/// <summary>
/// Provides handler lookup used during dispatch.
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
    /// <returns>The dispatcher registry.</returns>
    /// <exception cref="DuplicateHandlerException">A query or command has multiple handlers.</exception>
    [RequiresDynamicCode("Creating wrappers from runtime handler metadata requires dynamic generic construction.")]
    [RequiresUnreferencedCode("Creating wrappers from runtime handler metadata is not trimming safe.")]
    public static DispatcherRegistry Create(IEnumerable<HandlerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        return CreatePrepared(registrations.Select(PrepareRegistration));
    }

    /// <summary>
    /// Creates a registry from typed handler registrations.
    /// </summary>
    /// <param name="registrations">The handler registrations to include.</param>
    /// <returns>The dispatcher registry.</returns>
    /// <exception cref="DuplicateHandlerException">A query or command has multiple handlers.</exception>
    /// <exception cref="InvalidOperationException">A registration does not contain the required dispatch metadata.</exception>
    public static DispatcherRegistry CreatePrepared(IEnumerable<HandlerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var requests = new Dictionary<Type, (RequestHandlerWrapper Wrapper, Type HandlerType)>();
        var notifications = new Dictionary<Type, NotificationHandlerWrapper>();

        foreach (var registration in registrations)
        {
            ArgumentNullException.ThrowIfNull(registration);

            switch (registration)
            {
                case NotificationHandlerRegistration notification:
                    notifications.TryAdd(
                        notification.MessageType,
                        notification.Wrapper ??
                        throw MissingPreparedWrapper(notification.MessageType));
                    break;
                case QueryHandlerRegistration query:
                    AddRequest(requests, query, query.Wrapper);
                    break;
                case CommandWithResponseHandlerRegistration command:
                    AddRequest(requests, command, command.Wrapper);
                    break;
                case CommandHandlerRegistration command:
                    AddRequest(requests, command, command.Wrapper);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(registrations),
                        registration.GetType(),
                        "Unknown handler registration type.");
            }
        }

        return new DispatcherRegistry(
            requests.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.Wrapper),
            notifications.ToFrozenDictionary());
    }

    /// <summary>
    /// Prepares a handler registration for dispatch.
    /// </summary>
    /// <param name="registration">The handler registration to prepare.</param>
    /// <returns>The handler registration with its dispatch metadata.</returns>
    [RequiresDynamicCode("Creating wrappers from runtime handler metadata requires dynamic generic construction.")]
    [RequiresUnreferencedCode("Creating wrappers from runtime handler metadata is not trimming safe.")]
    public static HandlerRegistration PrepareRegistration(HandlerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return registration switch
        {
            QueryHandlerRegistration { Wrapper: not null } query => query,
            QueryHandlerRegistration query => query with
            {
                Wrapper = (RequestHandlerWrapper)CreateWrapper(
                    typeof(QueryHandlerWrapper<,>),
                    query.MessageType,
                    query.ResponseType)
            },
            CommandWithResponseHandlerRegistration { Wrapper: not null } command => command,
            CommandWithResponseHandlerRegistration command => command with
            {
                Wrapper = (RequestHandlerWrapper)CreateWrapper(
                    typeof(CommandWithResponseHandlerWrapper<,>),
                    command.MessageType,
                    command.ResponseType)
            },
            CommandHandlerRegistration { Wrapper: not null } command => command,
            CommandHandlerRegistration command => command with
            {
                Wrapper = (RequestHandlerWrapper)CreateWrapper(
                    typeof(CommandHandlerWrapper<>),
                    command.MessageType)
            },
            NotificationHandlerRegistration { Wrapper: not null } notification => notification,
            NotificationHandlerRegistration notification => notification with
            {
                Wrapper = (NotificationHandlerWrapper)CreateWrapper(
                    typeof(NotificationHandlerWrapper<>),
                    notification.MessageType)
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(registration),
                registration.GetType(),
                "Unknown handler registration type.")
        };
    }

    private static void AddRequest(
        Dictionary<Type, (RequestHandlerWrapper Wrapper, Type HandlerType)> requests,
        HandlerRegistration registration,
        RequestHandlerWrapper? wrapper)
    {
        var preparedWrapper = wrapper ??
            throw MissingPreparedWrapper(registration.MessageType);

        if (requests.TryAdd(registration.MessageType, (preparedWrapper, registration.HandlerType)))
        {
            return;
        }

        var existing = requests[registration.MessageType];
        throw new DuplicateHandlerException(
            registration.MessageType,
            existing.HandlerType,
            registration.HandlerType);
    }

    [RequiresDynamicCode("Creating wrappers from runtime handler metadata requires dynamic generic construction.")]
    [RequiresUnreferencedCode("Creating wrappers from runtime handler metadata is not trimming safe.")]
    private static object CreateWrapper(Type wrapperType, params Type[] genericArguments) =>
        Activator.CreateInstance(wrapperType.MakeGenericType(genericArguments))!;

    private static InvalidOperationException MissingPreparedWrapper(Type messageType) =>
        new($"Registration for '{messageType.FullName}' was not created by a typed registration method.");
}