namespace Dispatcher;

/// <summary>
/// Represents a command that can be executed through a request pipeline.
/// </summary>
public interface ICommandBase : IRequest;

/// <summary>
/// Represents a command that returns a response.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command.</typeparam>
public interface ICommand<TResponse> : ICommandBase;

/// <summary>
/// Represents a command that does not return a response.
/// </summary>
public interface ICommand : ICommandBase;