namespace Dispatcher;

/// <summary>
/// Identifies a command that returns a response.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface ICommand<out TResponse> : IRequest;

/// <summary>
/// Identifies a command that does not return a response.
/// </summary>
public interface ICommand : ICommand<Unit>;