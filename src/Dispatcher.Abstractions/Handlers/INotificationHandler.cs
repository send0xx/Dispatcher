namespace Dispatcher;

/// <summary>
/// Handles notifications of type <typeparamref name="TNotification"/>.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles a notification.
    /// </summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>An operation that represents notification handling.</returns>
    ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken);
}