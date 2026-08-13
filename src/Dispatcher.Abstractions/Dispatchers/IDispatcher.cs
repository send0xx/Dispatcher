namespace Dispatcher;

/// <summary>
/// Defines a dispatcher that combines query, command, and notification operations.
/// </summary>
public interface IDispatcher : IQueryDispatcher, ICommandDispatcher, INotificationDispatcher;