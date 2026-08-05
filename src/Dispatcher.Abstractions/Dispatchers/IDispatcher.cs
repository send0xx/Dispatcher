namespace Dispatcher;

/// <summary>
/// Dispatches queries and commands and publishes notifications.
/// </summary>
public interface IDispatcher : IQueryDispatcher, ICommandDispatcher, INotificationPublisher;