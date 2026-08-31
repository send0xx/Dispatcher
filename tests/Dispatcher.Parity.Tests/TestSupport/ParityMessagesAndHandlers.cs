using Dispatcher.SourceGeneration;

[assembly: GenerateDispatcherHandlers("AddParityHandlers")]
[assembly: GenerateDispatcher("AddGeneratedDispatcher")]

namespace Dispatcher.Parity.Tests.TestSupport;

/// <summary>
/// Records what each handler did, so a scenario can assert the exact sequence both implementations
/// are expected to produce.
/// </summary>
public sealed class ParityRecorder
{
    public List<string> Events { get; } = [];
}

public sealed record GreetQuery(string Name) : IQuery<string>;

internal sealed class GreetQueryHandler(ParityRecorder recorder) : IQueryHandler<GreetQuery, string>
{
    public ValueTask<string> HandleAsync(GreetQuery query, CancellationToken cancellationToken)
    {
        recorder.Events.Add("greet");
        return ValueTask.FromResult($"Hello, {query.Name}");
    }
}

internal sealed class GreetBehavior(ParityRecorder recorder) : IPipelineBehavior<GreetQuery, string>
{
    public async ValueTask<string> HandleAsync(
        GreetQuery request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        recorder.Events.Add("greet-before");
        var response = await next(cancellationToken);
        recorder.Events.Add("greet-after");
        return response;
    }
}

/// <summary>A query with two behaviors, so the pipeline order is observable.</summary>
public sealed record OrderedQuery : IQuery<string>;

internal sealed class OrderedQueryHandler(ParityRecorder recorder) : IQueryHandler<OrderedQuery, string>
{
    public ValueTask<string> HandleAsync(OrderedQuery query, CancellationToken cancellationToken)
    {
        recorder.Events.Add("ordered");
        return ValueTask.FromResult("ordered");
    }
}

internal sealed class FirstOrderedBehavior(ParityRecorder recorder) : IPipelineBehavior<OrderedQuery, string>
{
    public async ValueTask<string> HandleAsync(
        OrderedQuery request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        recorder.Events.Add("first-before");
        var response = await next(cancellationToken);
        recorder.Events.Add("first-after");
        return response;
    }
}

internal sealed class SecondOrderedBehavior(ParityRecorder recorder) : IPipelineBehavior<OrderedQuery, string>
{
    public async ValueTask<string> HandleAsync(
        OrderedQuery request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        recorder.Events.Add("second-before");
        var response = await next(cancellationToken);
        recorder.Events.Add("second-after");
        return response;
    }
}

/// <summary>A query whose behavior answers without invoking the handler.</summary>
public sealed record CachedQuery : IQuery<string>;

internal sealed class CachedQueryHandler(ParityRecorder recorder) : IQueryHandler<CachedQuery, string>
{
    public ValueTask<string> HandleAsync(CachedQuery query, CancellationToken cancellationToken)
    {
        recorder.Events.Add("cached-handler");
        return ValueTask.FromResult("handled");
    }
}

internal sealed class CachedQueryBehavior : IPipelineBehavior<CachedQuery, string>
{
    public ValueTask<string> HandleAsync(
        CachedQuery request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken) => ValueTask.FromResult("cached");
}

/// <summary>A resultless command with a behavior, so its adaptation to <see cref="Unit"/> is observable.</summary>
public sealed record TrackedCommand(string Value) : ICommand;

internal sealed class TrackedCommandHandler(ParityRecorder recorder) : ICommandHandler<TrackedCommand>
{
    public ValueTask HandleAsync(TrackedCommand command, CancellationToken cancellationToken)
    {
        recorder.Events.Add("tracked-" + command.Value);
        return ValueTask.CompletedTask;
    }
}

internal sealed class TrackedCommandBehavior(ParityRecorder recorder) : IPipelineBehavior<TrackedCommand, Unit>
{
    public async ValueTask<Unit> HandleAsync(
        TrackedCommand request,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken)
    {
        recorder.Events.Add("tracked-before");
        var response = await next(cancellationToken);
        recorder.Events.Add("tracked-after-" + response.Equals(Unit.Value));
        return response;
    }
}

/// <summary>A query whose handler observes the dispatched cancellation token.</summary>
public sealed record CancellationQuery : IQuery<string>;

internal sealed class CancellationQueryHandler : IQueryHandler<CancellationQuery, string>
{
    public ValueTask<string> HandleAsync(CancellationQuery query, CancellationToken cancellationToken) =>
        new(Task.FromCanceled<string>(cancellationToken));
}

public sealed record SumCommand(int Left, int Right) : ICommand<int>;

internal sealed class SumCommandHandler(ParityRecorder recorder) : ICommandHandler<SumCommand, int>
{
    public ValueTask<int> HandleAsync(SumCommand command, CancellationToken cancellationToken)
    {
        recorder.Events.Add("sum");
        return ValueTask.FromResult(command.Left + command.Right);
    }
}

public sealed record RecordCommand(string Value) : ICommand;

internal sealed class RecordCommandHandler(ParityRecorder recorder) : ICommandHandler<RecordCommand>
{
    public ValueTask HandleAsync(RecordCommand command, CancellationToken cancellationToken)
    {
        recorder.Events.Add("record-" + command.Value);
        return ValueTask.CompletedTask;
    }
}

public abstract record ReportQuery(string Value) : IQuery<string>;

/// <summary>A report with no handler of its own, so it must route to the base handler.</summary>
public sealed record DailyReportQuery(string Value) : ReportQuery(Value);

/// <summary>A report with its own handler, which must suppress the base handler.</summary>
public sealed record HourlyReportQuery(string Value) : ReportQuery(Value);

internal sealed class ReportQueryHandler : IQueryHandler<ReportQuery, string>
{
    public ValueTask<string> HandleAsync(ReportQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult("base " + query.Value);
}

internal sealed class HourlyReportQueryHandler : IQueryHandler<HourlyReportQuery, string>
{
    public ValueTask<string> HandleAsync(HourlyReportQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult("exact " + query.Value);
}

public abstract record DomainEvent : INotification;

/// <summary>An event with no handler of its own, so it must route to the base handlers.</summary>
public sealed record UserUpdated : DomainEvent;

/// <summary>An event with its own handler, which must suppress the base handlers.</summary>
public sealed record UserCreated : DomainEvent;

/// <summary>A value-type notification handled through a generated closed registration.</summary>
public readonly record struct AuditPulse : INotification;

/// <summary>A notification no handler can observe, so publishing it must do nothing.</summary>
public sealed record Ignored : INotification;

/// <summary>
/// A notification outside the <see cref="DomainEvent"/> hierarchy, so no open generic handler
/// applies and it exercises the closed-only notification route.
/// </summary>
public sealed record Heartbeat : INotification;

internal sealed class FirstHeartbeatHandler(ParityRecorder recorder) : INotificationHandler<Heartbeat>
{
    public ValueTask HandleAsync(Heartbeat notification, CancellationToken cancellationToken)
    {
        recorder.Events.Add("heartbeat-a");
        return ValueTask.CompletedTask;
    }
}

internal sealed class SecondHeartbeatHandler(ParityRecorder recorder) : INotificationHandler<Heartbeat>
{
    public ValueTask HandleAsync(Heartbeat notification, CancellationToken cancellationToken)
    {
        recorder.Events.Add("heartbeat-b");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A notification whose second handler throws, so the stop-remaining-handlers contract is observable.
/// It stays outside the <see cref="DomainEvent"/> hierarchy so no open generic handler applies.
/// </summary>
public sealed record AlertRaised : INotification;

internal sealed class FirstAlertHandler(ParityRecorder recorder) : INotificationHandler<AlertRaised>
{
    public ValueTask HandleAsync(AlertRaised notification, CancellationToken cancellationToken)
    {
        recorder.Events.Add("alert-a");
        return ValueTask.CompletedTask;
    }
}

internal sealed class SecondAlertHandler : INotificationHandler<AlertRaised>
{
    public ValueTask HandleAsync(AlertRaised notification, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("alert failed");
}

internal sealed class ThirdAlertHandler(ParityRecorder recorder) : INotificationHandler<AlertRaised>
{
    public ValueTask HandleAsync(AlertRaised notification, CancellationToken cancellationToken)
    {
        recorder.Events.Add("alert-c");
        return ValueTask.CompletedTask;
    }
}

internal sealed class FirstDomainEventHandler(ParityRecorder recorder) : INotificationHandler<DomainEvent>
{
    public ValueTask HandleAsync(DomainEvent notification, CancellationToken cancellationToken)
    {
        recorder.Events.Add("domain-a");
        return ValueTask.CompletedTask;
    }
}

internal sealed class SecondDomainEventHandler(ParityRecorder recorder) : INotificationHandler<DomainEvent>
{
    public ValueTask HandleAsync(DomainEvent notification, CancellationToken cancellationToken)
    {
        recorder.Events.Add("domain-b");
        return ValueTask.CompletedTask;
    }
}

internal sealed class UserCreatedHandler(ParityRecorder recorder) : INotificationHandler<UserCreated>
{
    public ValueTask HandleAsync(UserCreated notification, CancellationToken cancellationToken)
    {
        recorder.Events.Add("user-created");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Observes every <see cref="DomainEvent"/>, closed over the concrete published type. The constraint
/// keeps <see cref="Ignored"/> unobserved, so the no-handler case stays testable.
/// </summary>
public sealed class AuditHandler<TNotification>(ParityRecorder recorder)
    : INotificationHandler<TNotification>
    where TNotification : DomainEvent
{
    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        recorder.Events.Add("audit-" + typeof(TNotification).Name);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Observes value-type notifications through a generated closed registration.</summary>
public sealed class StructAuditHandler<TNotification>(ParityRecorder recorder)
    : INotificationHandler<TNotification>
    where TNotification : struct, INotification
{
    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        recorder.Events.Add("audit-" + typeof(TNotification).Name);
        return ValueTask.CompletedTask;
    }
}