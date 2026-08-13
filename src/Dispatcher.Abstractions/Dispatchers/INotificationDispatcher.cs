namespace Dispatcher;

/// <summary>
/// Defines operations for publishing notifications to their registered handlers.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Publishes a notification to all registered handlers in registration order.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to publish.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A value task that represents the asynchronous notification publication.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="notification"/> is <see langword="null"/>.
    /// </exception>
    ValueTask PublishAsync<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification;
}