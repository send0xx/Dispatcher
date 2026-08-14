namespace Dispatcher;

/// <summary>
/// Represents metadata for a message that can participate in dispatch routing.
/// </summary>
public record MessageRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRegistration"/> class.
    /// </summary>
    /// <param name="messageType">The message type that can participate in routing.</param>
    public MessageRegistration(Type messageType)
    {
        MessageType = messageType;
    }

    /// <summary>
    /// Gets the message type.
    /// </summary>
    /// <value>
    /// The concrete route target, handled message type, or open notification handler type parameter.
    /// </value>
    public Type MessageType { get; }
}