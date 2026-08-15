namespace Dispatcher;

/// <summary>
/// Represents a query that can be dispatched through a request pipeline.
/// </summary>
public interface IQueryBase : IRequest;

/// <summary>
/// Represents a query that returns a response.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the query.</typeparam>
public interface IQuery<out TResponse> : IQueryBase;