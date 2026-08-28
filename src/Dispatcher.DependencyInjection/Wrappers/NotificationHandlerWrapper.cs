namespace Dispatcher;

/// <summary>
/// Defines an executable notification route prepared during registry creation.
/// </summary>
internal abstract class NotificationHandlerWrapper
{
    public abstract ValueTask HandleAsync(
        object notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Invokes the closed notification handlers registered for the selected handled notification type.
/// </summary>
/// <typeparam name="TNotification">The selected handled notification type.</typeparam>
internal sealed class NotificationHandlerWrapper<TNotification> : NotificationHandlerWrapper
    where TNotification : INotification
{
    public override async ValueTask HandleAsync(
        object notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>();

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var index = 0; index < handlers.Count; index++)
        {
            await handlers[index].HandleAsync((TNotification)notification, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Invokes compatible open generic notification handlers for a concrete published notification type
/// when no closed notification route is selected.
/// </summary>
/// <param name="handlerTypes">
/// The open handler implementation types closed over <typeparamref name="TNotification"/> during registry creation.
/// </param>
/// <typeparam name="TNotification">The concrete published notification type.</typeparam>
internal sealed class OpenNotificationHandlerWrapper<TNotification>(Type[] handlerTypes)
    : NotificationHandlerWrapper
    where TNotification : INotification
{
    public override async ValueTask HandleAsync(
        object notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < handlerTypes.Length; index++)
        {
            var handler = (INotificationHandler<TNotification>?)serviceProvider.GetService(handlerTypes[index]) ??
                          throw new InvalidOperationException(
                              $"Service '{handlerTypes[index].FullName}' is not registered.");
            await handler.HandleAsync((TNotification)notification, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Invokes the closed handlers for the selected handled notification type followed by compatible
/// open generic handlers closed over the concrete published notification type.
/// </summary>
/// <param name="handlerTypes">
/// The open handler implementation types closed over <typeparamref name="TNotification"/> during registry creation.
/// </param>
/// <typeparam name="THandledNotification">
/// The notification type selected by exact or polymorphic closed-route resolution.
/// </typeparam>
/// <typeparam name="TNotification">The concrete published notification type.</typeparam>
internal sealed class CompositeNotificationHandlerWrapper<THandledNotification, TNotification>(Type[] handlerTypes)
    : NotificationHandlerWrapper
    where THandledNotification : INotification
    where TNotification : INotification
{
    public override async ValueTask HandleAsync(
        object notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var closedHandlers = serviceProvider.GetServices<INotificationHandler<THandledNotification>>();
        for (var index = 0; index < closedHandlers.Count; index++)
        {
            await closedHandlers[index]
                .HandleAsync((THandledNotification)notification, cancellationToken)
                .ConfigureAwait(false);
        }

        for (var index = 0; index < handlerTypes.Length; index++)
        {
            var handler = (INotificationHandler<TNotification>?)serviceProvider.GetService(handlerTypes[index]) ??
                          throw new InvalidOperationException(
                              $"Service '{handlerTypes[index].FullName}' is not registered.");
            await handler.HandleAsync((TNotification)notification, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Records telemetry around an executable notification route.
/// </summary>
/// <param name="inner">The prepared notification route to invoke.</param>
/// <param name="route">The telemetry route associated with the concrete published notification type.</param>
internal sealed class TelemetryNotificationHandlerWrapper(
    NotificationHandlerWrapper inner,
    DispatcherTelemetryRoute route) : NotificationHandlerWrapper
{
    public override async ValueTask HandleAsync(
        object notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var telemetryScope = route.Start();
        try
        {
            await inner.HandleAsync(notification, serviceProvider, cancellationToken).ConfigureAwait(false);
            telemetryScope.Complete();
        }
        catch (Exception exception)
        {
            telemetryScope.Fail(exception);
            throw;
        }
    }
}