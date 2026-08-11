namespace Dispatcher;

internal abstract class NotificationHandlerWrapper
{
    public abstract ValueTask HandleAsync(
        object notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

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

internal sealed class TelemetryNotificationHandlerWrapper(
    NotificationHandlerWrapper inner,
    DispatcherTelemetryRoute route) : NotificationHandlerWrapper
{
    public override ValueTask HandleAsync(
        object notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var telemetryScope = route.Start();
        try
        {
            var response = inner.HandleAsync(notification, serviceProvider, cancellationToken);
            if (!response.IsCompletedSuccessfully)
            {
                return Awaited(response, telemetryScope);
            }

            response.GetAwaiter().GetResult();
            telemetryScope.Complete();
            return ValueTask.CompletedTask;
        }
        catch (Exception exception)
        {
            telemetryScope.Fail(exception);
            throw;
        }
    }

    private static async ValueTask Awaited(
        ValueTask response,
        DispatcherTelemetryScope telemetryScope)
    {
        try
        {
            await response.ConfigureAwait(false);
            telemetryScope.Complete();
        }
        catch (Exception exception)
        {
            telemetryScope.Fail(exception);
            throw;
        }
    }
}
