namespace Dispatcher;

/// <summary>
/// Identifies a query that returns a response.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IQuery<out TResponse> : IRequest;