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
        => CreatePrepared(registrations, telemetry: null);

    internal static DispatcherRegistry CreatePrepared(
        IEnumerable<HandlerRegistration> registrations,
        DispatcherTelemetry? telemetry)
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
                    var notificationFactory = notification.WrapperFactory ??
                        throw MissingPreparedWrapper(notification.MessageType);
                    notifications.TryAdd(
                        notification.MessageType,
                        notificationFactory.Create(telemetry));
                    break;
                case QueryHandlerRegistration query:
                    AddRequest(requests, query, query.WrapperFactory, telemetry);
                    break;
                case CommandWithResponseHandlerRegistration command:
                    AddRequest(requests, command, command.WrapperFactory, telemetry);
                    break;
                case CommandHandlerRegistration command:
                    AddRequest(requests, command, command.WrapperFactory, telemetry);
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
            QueryHandlerRegistration { WrapperFactory: not null } query => query,
            QueryHandlerRegistration query => query with
            {
                WrapperFactory = (RequestHandlerWrapperFactory)CreateWrapperFactory(
                    typeof(QueryHandlerWrapperFactory<,>),
                    query.MessageType,
                    query.ResponseType)
            },
            CommandWithResponseHandlerRegistration { WrapperFactory: not null } command => command,
            CommandWithResponseHandlerRegistration command => command with
            {
                WrapperFactory = (RequestHandlerWrapperFactory)CreateWrapperFactory(
                    typeof(CommandWithResponseHandlerWrapperFactory<,>),
                    command.MessageType,
                    command.ResponseType)
            },
            CommandHandlerRegistration { WrapperFactory: not null } command => command,
            CommandHandlerRegistration command => command with
            {
                WrapperFactory = (RequestHandlerWrapperFactory)CreateWrapperFactory(
                    typeof(CommandHandlerWrapperFactory<>),
                    command.MessageType)
            },
            NotificationHandlerRegistration { WrapperFactory: not null } notification => notification,
            NotificationHandlerRegistration notification => notification with
            {
                WrapperFactory = (NotificationHandlerWrapperFactory)CreateWrapperFactory(
                    typeof(NotificationHandlerWrapperFactory<>),
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
        RequestHandlerWrapperFactory? wrapperFactory,
        DispatcherTelemetry? telemetry)
    {
        var preparedWrapper = (wrapperFactory ??
            throw MissingPreparedWrapper(registration.MessageType)).Create(telemetry);

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
    private static object CreateWrapperFactory(Type factoryType, params Type[] genericArguments) =>
        Activator.CreateInstance(factoryType.MakeGenericType(genericArguments))!;

    private static InvalidOperationException MissingPreparedWrapper(Type messageType) =>
        new($"Registration for '{messageType.FullName}' was not created by a typed registration method.");
}
