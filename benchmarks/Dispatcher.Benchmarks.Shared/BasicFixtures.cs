using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks.Shared;

public sealed record PingQuery(int Value) : IQuery<int>;

public sealed class DirectPingHandler
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask<int> HandleAsync(PingQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(query.Value + 1);
}

internal sealed class PingQueryHandler(ScopedProbe probe) : IQueryHandler<PingQuery, int>
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask<int> HandleAsync(PingQuery query, CancellationToken cancellationToken)
    {
        _ = probe.Id;
        return ValueTask.FromResult(query.Value + 1);
    }
}

public sealed record IncrementCommand(int Value) : ICommand<int>;

internal sealed class IncrementCommandHandler(ScopedProbe probe) : ICommandHandler<IncrementCommand, int>
{
    public ValueTask<int> HandleAsync(IncrementCommand command, CancellationToken cancellationToken)
    {
        _ = probe.Id;
        return ValueTask.FromResult(command.Value + 1);
    }
}

public sealed record TouchCommand : ICommand;

internal sealed class TouchCommandHandler(ScopedProbe probe) : ICommandHandler<TouchCommand>
{
    public ValueTask HandleAsync(TouchCommand command, CancellationToken cancellationToken)
    {
        _ = probe.Id;
        return ValueTask.CompletedTask;
    }
}

public sealed record TouchedNotification : INotification;

internal sealed class TouchedNotificationHandler(ScopedProbe probe) : INotificationHandler<TouchedNotification>
{
    public ValueTask HandleAsync(TouchedNotification notification, CancellationToken cancellationToken)
    {
        _ = probe.Id;
        return ValueTask.CompletedTask;
    }
}

public sealed record TouchedTwiceNotification : INotification;

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

public sealed record FailingQuery : IQuery<int>;

internal sealed class FailingQueryHandler : IQueryHandler<FailingQuery, int>
{
    public ValueTask<int> HandleAsync(FailingQuery query, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Expected benchmark failure.");
}

public static class BasicHandlerRegistration
{
    public static void Add(IServiceCollection services)
    {
        services.AddQueryHandler<PingQuery, int, PingQueryHandler>();
        services.AddCommandHandler<IncrementCommand, int, IncrementCommandHandler>();
        services.AddCommandHandler<TouchCommand, TouchCommandHandler>();
        services.AddNotificationHandler<TouchedNotification, TouchedNotificationHandler>();
        services.AddNotificationHandler<TouchedTwiceNotification, FirstTouchedTwiceNotificationHandler>();
        services.AddNotificationHandler<TouchedTwiceNotification, SecondTouchedTwiceNotificationHandler>();
        services.AddQueryHandler<FailingQuery, int, FailingQueryHandler>();
    }
}