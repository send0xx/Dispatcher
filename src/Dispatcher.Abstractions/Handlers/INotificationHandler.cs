namespace Dispatcher;

/// <summary>
/// Defines a handler for notifications of type <typeparamref name="TNotification"/>.
/// </summary>
/// <typeparam name="TNotification">The type of notification to handle.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles a notification.
    /// </summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A value task that represents the asynchronous notification handling operation.</returns>
    ValueTask HandleAsync(
        TNotification notification,
        CancellationToken cancellationToken = default);
}