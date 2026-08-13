using Dispatcher.NativeAotHostSample.Contracts;
using Dispatcher.SourceGeneration;

[assembly: GenerateDispatcherHandlers("AddAuditHandlers")]

namespace Dispatcher.NativeAotHostSample.Audit;

public sealed record GetAuditCountQuery : IQuery<int>;

public sealed class AuditState
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Record() => Interlocked.Increment(ref _count);
}

internal sealed class GetAuditCountQueryHandler(AuditState state)
    : IQueryHandler<GetAuditCountQuery, int>
{
    public ValueTask<int> HandleAsync(
        GetAuditCountQuery query,
        CancellationToken cancellationToken) => ValueTask.FromResult(state.Count);
}

internal sealed class AuditMessageAddedHandler(AuditState state)
    : INotificationHandler<MessageEvent>
{
    public ValueTask HandleAsync(
        MessageEvent notification,
        CancellationToken cancellationToken)
    {
        state.Record();
        return ValueTask.CompletedTask;
    }
}