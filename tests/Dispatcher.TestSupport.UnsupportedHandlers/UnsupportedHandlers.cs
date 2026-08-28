namespace Dispatcher.TestSupport.UnsupportedHandlers;

/// <summary>
/// Handlers in this assembly are deliberately unregistrable. They live in their own assembly so
/// that scanning them cannot affect the tests that scan the supported handler assemblies.
/// </summary>
public sealed class UnsupportedHandlerAssemblyMarker;

public sealed record UnsupportedPing : INotification;

public sealed record UnsupportedLookup(int Id) : IQuery<string>;

public sealed record UnsupportedLookup<TKey>(TKey Key) : IQuery<string>;

public sealed class GenericHandlerWithClosedNotification<TState> : INotificationHandler<UnsupportedPing>
{
    public ValueTask HandleAsync(UnsupportedPing notification, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}

public sealed class OpenGenericQueryHandler<TKey> : IQueryHandler<UnsupportedLookup<TKey>, string>
{
    public ValueTask<string> HandleAsync(
        UnsupportedLookup<TKey> query,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult("unsupported");
}

public sealed class OpenNotificationHandlerWithoutPublicConstructor<TNotification>
    : INotificationHandler<TNotification>
    where TNotification : INotification
{
    private OpenNotificationHandlerWithoutPublicConstructor()
    {
    }

    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}

public sealed class ClosedHandlerWithoutPublicConstructor : IQueryHandler<UnsupportedLookup, string>
{
    private ClosedHandlerWithoutPublicConstructor()
    {
    }

    public ValueTask<string> HandleAsync(UnsupportedLookup query, CancellationToken cancellationToken) =>
        ValueTask.FromResult("unsupported");
}