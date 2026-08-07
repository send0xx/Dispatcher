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