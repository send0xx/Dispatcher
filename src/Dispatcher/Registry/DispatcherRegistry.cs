using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

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
    [RequiresDynamicCode("Creating wrappers from runtime handler metadata requires dynamic generic construction.")]
    [RequiresUnreferencedCode("Creating wrappers from runtime handler metadata is not trimming safe.")]
    public static DispatcherRegistry Create(IEnumerable<HandlerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        return CreatePrepared(registrations.Select(PrepareRegistration));
    }

    /// <summary>
    /// Creates a registry from registrations that already contain closed dispatch wrappers.
    /// </summary>
    /// <param name="registrations">The prepared handler registrations to include.</param>
    /// <returns>An immutable dispatcher registry.</returns>
    /// <exception cref="DuplicateHandlerException">A query or command has multiple handlers.</exception>
    /// <exception cref="InvalidOperationException">A registration was not created by a typed factory or prepared first.</exception>
    public static DispatcherRegistry CreatePrepared(IEnumerable<HandlerRegistration> registrations)
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
                    registration.NotificationWrapper ??
                    throw MissingPreparedWrapper(registration.MessageType));
                continue;
            }

            var wrapper = registration.RequestWrapper ??
                throw MissingPreparedWrapper(registration.MessageType);
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

    /// <summary>
    /// Creates and attaches a dispatch wrapper using runtime handler metadata.
    /// </summary>
    /// <param name="registration">The handler registration to prepare.</param>
    /// <returns>A registration containing its closed dispatch wrapper.</returns>
    [RequiresDynamicCode("Creating wrappers from runtime handler metadata requires dynamic generic construction.")]
    [RequiresUnreferencedCode("Creating wrappers from runtime handler metadata is not trimming safe.")]
    public static HandlerRegistration PrepareRegistration(HandlerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (registration.RequestWrapper is not null || registration.NotificationWrapper is not null)
        {
            return registration;
        }

        var wrapperType = registration.Kind switch
        {
            HandlerKind.Query => typeof(QueryHandlerWrapper<,>).MakeGenericType(
                registration.MessageType,
                registration.ResponseType ?? throw MissingResponseType(registration)),
            HandlerKind.CommandWithResponse => typeof(CommandWithResponseHandlerWrapper<,>).MakeGenericType(
                registration.MessageType,
                registration.ResponseType ?? throw MissingResponseType(registration)),
            HandlerKind.Command => typeof(CommandHandlerWrapper<>).MakeGenericType(registration.MessageType),
            HandlerKind.Notification => typeof(NotificationHandlerWrapper<>).MakeGenericType(
                registration.MessageType),
            _ => throw new ArgumentOutOfRangeException(nameof(registration), registration.Kind, "Unknown handler kind.")
        };

        var wrapper = Activator.CreateInstance(wrapperType)!;

        return registration.Kind == HandlerKind.Notification
            ? registration with
            {
                NotificationWrapper = (NotificationHandlerWrapper)wrapper
            }
            : registration with
            {
                RequestWrapper = (RequestHandlerWrapper)wrapper
            };
    }

    private static InvalidOperationException MissingResponseType(HandlerRegistration registration) =>
        new($"Registration for '{registration.MessageType.FullName}' requires a response type.");

    private static InvalidOperationException MissingPreparedWrapper(Type messageType) =>
        new($"Registration for '{messageType.FullName}' was not created by a typed registration method.");
}