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
}