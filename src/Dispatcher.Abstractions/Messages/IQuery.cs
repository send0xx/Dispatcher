namespace Dispatcher;

public interface IQuery<out TResponse> : IRequest;