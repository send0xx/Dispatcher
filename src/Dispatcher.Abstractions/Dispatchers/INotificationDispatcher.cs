namespace Dispatcher;

/// <summary>
/// Dispatches notifications to their registered handlers.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Publishes a notification to all registered handlers.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>An operation that represents notification publication.</returns>
    ValueTask PublishAsync<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification;
}