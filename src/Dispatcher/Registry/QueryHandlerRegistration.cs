namespace Dispatcher;

/// <summary>
/// Represents a query handler registration.
/// </summary>
public sealed record QueryHandlerRegistration : HandlerRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryHandlerRegistration"/> class.
    /// </summary>
    /// <param name="messageType">The handled query type.</param>
    /// <param name="responseType">The query response type.</param>
    /// <param name="handlerType">The concrete handler type.</param>
    public QueryHandlerRegistration(Type messageType, Type responseType, Type handlerType)
        : base(messageType, handlerType)
    {
        ResponseType = responseType;
    }

    /// <summary>
    /// Gets the query response type.
    /// </summary>
    /// <value>The type of response returned by the query.</value>
    public Type ResponseType { get; }

    /// <summary>
    /// Creates a query handler registration.
    /// </summary>
    /// <typeparam name="TQuery">The type of query to register.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the query.</typeparam>
    /// <typeparam name="THandler">The type of query handler to register.</typeparam>
    /// <returns>A query handler registration for the specified types.</returns>
    public static QueryHandlerRegistration Create<TQuery, TResponse, THandler>()
        where TQuery : IQuery<TResponse>
        where THandler : class, IQueryHandler<TQuery, TResponse> =>
        new(typeof(TQuery), typeof(TResponse), typeof(THandler));
}