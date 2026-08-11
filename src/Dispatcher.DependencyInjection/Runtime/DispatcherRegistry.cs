using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Dispatcher;

/// <summary>
/// Provides handler lookup used by the reflection-based Dispatcher implementation.
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
    /// Creates a registry from handler registration metadata.
    /// </summary>
    /// <param name="registrations">The handler registrations to include.</param>
    /// <returns>The dispatcher registry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registrations"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateHandlerException">A query or command has multiple handlers.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A registration has an unknown metadata type.</exception>
    [RequiresDynamicCode("Creating handler wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from registration metadata is not trimming safe.")]
    public static DispatcherRegistry Create(IEnumerable<HandlerRegistration> registrations) =>
        CreateCore(registrations, telemetry: null);

    /// <summary>
    /// Creates a registry from handler registration metadata with telemetry instrumentation.
    /// </summary>
    /// <param name="registrations">The handler registrations to include.</param>
    /// <param name="telemetry">The telemetry service used to instrument routed handlers.</param>
    /// <returns>The dispatcher registry.</returns>
    /// <remarks>The caller retains ownership of <paramref name="telemetry"/> and must dispose it.</remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="registrations"/> or <paramref name="telemetry"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="DuplicateHandlerException">A query or command has multiple handlers.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A registration has an unknown metadata type.</exception>
    [RequiresDynamicCode("Creating handler wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from registration metadata is not trimming safe.")]
    public static DispatcherRegistry Create(
        IEnumerable<HandlerRegistration> registrations,
        DispatcherTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        return CreateCore(registrations, telemetry);
    }

    [RequiresDynamicCode("Creating handler wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from registration metadata is not trimming safe.")]
    private static DispatcherRegistry CreateCore(
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
                    if (!notifications.ContainsKey(notification.MessageType))
                    {
                        var factory = (NotificationHandlerWrapperFactory)CreateWrapperFactory(
                            typeof(NotificationHandlerWrapperFactory<>),
                            notification.MessageType);
                        notifications.Add(notification.MessageType, factory.Create(telemetry));
                    }

                    break;
                case QueryHandlerRegistration query:
                    AddRequest(
                        requests,
                        query,
                        (RequestHandlerWrapperFactory)CreateWrapperFactory(
                            typeof(QueryHandlerWrapperFactory<,>),
                            query.MessageType,
                            query.ResponseType),
                        telemetry);
                    break;
                case CommandWithResponseHandlerRegistration command:
                    AddRequest(
                        requests,
                        command,
                        (RequestHandlerWrapperFactory)CreateWrapperFactory(
                            typeof(CommandWithResponseHandlerWrapperFactory<,>),
                            command.MessageType,
                            command.ResponseType),
                        telemetry);
                    break;
                case CommandHandlerRegistration command:
                    AddRequest(
                        requests,
                        command,
                        (RequestHandlerWrapperFactory)CreateWrapperFactory(
                            typeof(CommandHandlerWrapperFactory<>),
                            command.MessageType),
                        telemetry);
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

    private static void AddRequest(
        Dictionary<Type, (RequestHandlerWrapper Wrapper, Type HandlerType)> requests,
        HandlerRegistration registration,
        RequestHandlerWrapperFactory wrapperFactory,
        DispatcherTelemetry? telemetry)
    {
        if (requests.TryAdd(
                registration.MessageType,
                (wrapperFactory.Create(telemetry), registration.HandlerType)))
        {
            return;
        }

        var existing = requests[registration.MessageType];
        throw new DuplicateHandlerException(
            registration.MessageType,
            existing.HandlerType,
            registration.HandlerType);
    }

    [RequiresDynamicCode("Creating handler wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from registration metadata is not trimming safe.")]
    private static object CreateWrapperFactory(Type factoryType, params Type[] genericArguments) =>
        Activator.CreateInstance(factoryType.MakeGenericType(genericArguments))!;
}
