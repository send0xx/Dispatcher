using System.Runtime.CompilerServices;

namespace Dispatcher.Benchmarks;

// The messages, handlers, and behaviors every benchmark class dispatches. They live together so
// that dispatch, implementation-comparison, and telemetry benchmarks measure the same shapes.

internal sealed record PingQuery(int Value) : IQuery<int>;

internal sealed class PingQueryHandler : IQueryHandler<PingQuery, int>
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask<int> HandleAsync(PingQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(query.Value + 1);
}

internal sealed record IncrementCommand(int Value) : ICommand<int>;

internal sealed class IncrementCommandHandler : ICommandHandler<IncrementCommand, int>
{
    public ValueTask<int> HandleAsync(IncrementCommand command, CancellationToken cancellationToken) =>
        ValueTask.FromResult(command.Value + 1);
}

internal sealed record TouchCommand : ICommand;

internal sealed class TouchCommandHandler : ICommandHandler<TouchCommand>
{
    public ValueTask HandleAsync(TouchCommand command, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}

internal sealed record TouchedNotification : INotification;

internal sealed class TouchedNotificationHandler : INotificationHandler<TouchedNotification>
{
    public ValueTask HandleAsync(TouchedNotification notification, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}

internal sealed record TouchedTwiceNotification : INotification;

internal sealed class FirstTouchedTwiceNotificationHandler : INotificationHandler<TouchedTwiceNotification>
{
    public ValueTask HandleAsync(TouchedTwiceNotification notification, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}

internal sealed class SecondTouchedTwiceNotificationHandler : INotificationHandler<TouchedTwiceNotification>
{
    public ValueTask HandleAsync(TouchedTwiceNotification notification, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}

/// <summary>
/// A behavior that adds a pipeline level and nothing else, for any request shape.
/// </summary>
internal sealed class PassthroughBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest
{
    public ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

// A pipeline of several behaviors needs a distinct type per level, because AddPipelineBehavior is
// idempotent and would otherwise register one behavior however many times it is called.

internal sealed class FirstPassthroughBehavior : IPipelineBehavior<PingQuery, int>
{
    public ValueTask<int> HandleAsync(
        PingQuery request,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

internal sealed class SecondPassthroughBehavior : IPipelineBehavior<PingQuery, int>
{
    public ValueTask<int> HandleAsync(
        PingQuery request,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

internal sealed class ThirdPassthroughBehavior : IPipelineBehavior<PingQuery, int>
{
    public ValueTask<int> HandleAsync(
        PingQuery request,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

/// <summary>
/// A handled base notification for the emitted module messages. It is abstract, so scanning the
/// benchmark assembly itself never treats it as a concrete route target.
/// </summary>
public abstract class ModuleEvent : INotification;

/// <summary>
/// Identifies notifications accepted by the unmatched open handler benchmark.
/// </summary>
public interface IUnmatchedNotification : INotification;

/// <summary>
/// An open handler whose constraint none of the emitted module notifications satisfies.
/// </summary>
/// <typeparam name="TNotification">The constrained notification type.</typeparam>
public sealed class UnmatchedOpenNotificationHandler<TNotification>
    : INotificationHandler<TNotification>
    where TNotification : IUnmatchedNotification
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}