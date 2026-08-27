using Dispatcher.TestSupport.Contracts;

namespace Dispatcher.TestSupport.AdditionalHandlers;

public sealed class AdditionalHandlerAssemblyMarker;

public sealed class LaterBaseQueryHandler : IQueryHandler<LaterBaseQuery, string>
{
    public ValueTask<string> HandleAsync(
        LaterBaseQuery query,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult("Handled later " + query.Value);
}

public sealed class OpenNotificationRecorder
{
    public List<string> Events { get; } = [];
}

public sealed class FirstOpenNotificationHandler<TNotification>(OpenNotificationRecorder? recorder = null)
    : INotificationHandler<TNotification>
    where TNotification : INotification
{
    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        recorder?.Events.Add("open-a-" + typeof(TNotification).Name);
        return ValueTask.CompletedTask;
    }
}

public sealed class SecondOpenNotificationHandler<TNotification>(OpenNotificationRecorder? recorder = null)
    : INotificationHandler<TNotification>
    where TNotification : INotification
{
    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        recorder?.Events.Add("open-b-" + typeof(TNotification).Name);
        return ValueTask.CompletedTask;
    }
}

public sealed class SharedOpenNotificationHandler<TNotification>(OpenNotificationRecorder? recorder = null)
    : INotificationHandler<TNotification>
    where TNotification : SharedNotification
{
    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        recorder?.Events.Add("open-shared-" + typeof(TNotification).Name);
        return ValueTask.CompletedTask;
    }
}

public sealed class RestrictedOpenNotificationHandler<TNotification>(OpenNotificationRecorder? recorder = null)
    : INotificationHandler<TNotification>
    where TNotification : IRestrictedNotification
{
    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        recorder?.Events.Add("open-restricted-" + typeof(TNotification).Name);
        return ValueTask.CompletedTask;
    }
}

internal sealed class SharedNotificationHandler(OpenNotificationRecorder? recorder = null)
    : INotificationHandler<SharedNotification>
{
    public ValueTask HandleAsync(SharedNotification notification, CancellationToken cancellationToken)
    {
        recorder?.Events.Add("closed-base");
        return ValueTask.CompletedTask;
    }
}

internal sealed class ExactSharedNotificationHandler(OpenNotificationRecorder? recorder = null)
    : INotificationHandler<ExactSharedNotification>
{
    public ValueTask HandleAsync(ExactSharedNotification notification, CancellationToken cancellationToken)
    {
        recorder?.Events.Add("closed-exact");
        return ValueTask.CompletedTask;
    }
}