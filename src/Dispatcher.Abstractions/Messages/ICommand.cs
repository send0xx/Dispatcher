namespace Dispatcher;

public interface ICommand<out TResponse> : IRequest;

public interface ICommand : ICommand<Unit>;