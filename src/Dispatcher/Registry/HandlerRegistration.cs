namespace Dispatcher;

/// <summary>
/// Describes a handler discovered during application registration.
/// </summary>
/// <param name="MessageType">The handled message type.</param>
/// <param name="ResponseType">The response type, or <see langword="null"/> when the handler has no response.</param>
/// <param name="Kind">The handler kind.</param>
/// <param name="HandlerType">The concrete handler type.</param>
public sealed record HandlerRegistration(
    Type MessageType,
    Type? ResponseType,
    HandlerKind Kind,
    Type HandlerType);