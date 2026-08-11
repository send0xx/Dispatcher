namespace Dispatcher;

/// <summary>
/// Describes a query handler registration.
/// </summary>
public sealed record QueryHandlerRegistration : HandlerRegistration
{
    /// <summary>
    /// Initializes a query handler registration.
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
    public Type ResponseType { get; }

    internal RequestHandlerWrapperFactory? WrapperFactory { get; init; }

    /// <summary>
    /// Creates a query handler registration.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The query handler implementation type.</typeparam>
    /// <returns>The query handler registration.</returns>
    public static QueryHandlerRegistration Create<TQuery, TResponse, THandler>()
        where TQuery : IQuery<TResponse>
        where THandler : class, IQueryHandler<TQuery, TResponse> =>
        new(typeof(TQuery), typeof(TResponse), typeof(THandler))
        {
            WrapperFactory = new QueryHandlerWrapperFactory<TQuery, TResponse>()
        };
}
