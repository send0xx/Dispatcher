namespace Dispatcher;

public interface IDispatcher : IQueryDispatcher, ICommandDispatcher, INotificationPublisher;