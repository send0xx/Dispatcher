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

        foreach (var handler in handlers)
        {
            await handler.HandleAsync((TNotification)notification, cancellationToken).ConfigureAwait(false);
        }
    }
}