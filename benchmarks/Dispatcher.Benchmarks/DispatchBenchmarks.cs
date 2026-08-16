using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Dispatcher.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "RatioSD")]
public class DispatchBenchmarks
{
    private static readonly PingQuery QueryMessage = new(41);
    private static readonly IncrementCommand CommandWithResponse = new(41);
    private static readonly TouchCommand Command = new();
    private static readonly TouchedNotification Notification = new();
    private static readonly TouchedTwiceNotification MultiHandlerNotification = new();

    private readonly PingQueryHandler _queryHandler = new();
    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private IDispatcher _dispatcher = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services
            .AddDispatcher()
            .AddDispatcherHandlers<DispatchBenchmarks>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _dispatcher = _scope.ServiceProvider.GetRequiredService<IDispatcher>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    [Benchmark(Baseline = true)]
    public ValueTask<int> DirectQueryHandler() =>
        _queryHandler.HandleAsync(QueryMessage, CancellationToken.None);

    [Benchmark]
    public ValueTask<int> Query() =>
        _dispatcher.QueryAsync(QueryMessage);

    [Benchmark]
    public ValueTask<int> CommandReturningResponse() =>
        _dispatcher.ExecuteAsync(CommandWithResponse);

    [Benchmark]
    public ValueTask CommandWithoutResponse() =>
        _dispatcher.ExecuteAsync(Command);

    [Benchmark]
    public ValueTask NotificationWithOneHandler() =>
        _dispatcher.PublishAsync(Notification);

    [Benchmark]
    public ValueTask NotificationWithTwoHandlers() =>
        _dispatcher.PublishAsync(MultiHandlerNotification);
}

[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "RatioSD")]
public class PipelineBenchmarks
{
    private static readonly PingQuery QueryMessage = new(41);

    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private IDispatcher _dispatcher = null!;

    // Every behavior has to be a distinct type. AddPipelineBehavior is idempotent, so registering one
    // type repeatedly measures a single behavior however high BehaviorCount is.
    private static readonly Action<IServiceCollection>[] BehaviorRegistrations =
    [
        static services => services.AddPipelineBehavior<FirstPassthroughBehavior>(),
        static services => services.AddPipelineBehavior<SecondPassthroughBehavior>(),
        static services => services.AddPipelineBehavior<ThirdPassthroughBehavior>()
    ];

    [Params(0, 1, 3)]
    public int BehaviorCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services
            .AddDispatcher()
            .AddDispatcherHandlers<DispatchBenchmarks>();

        for (var index = 0; index < BehaviorCount; index++)
        {
            BehaviorRegistrations[index](services);
        }

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _dispatcher = _scope.ServiceProvider.GetRequiredService<IDispatcher>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    [Benchmark]
    public ValueTask<int> Query() =>
        _dispatcher.QueryAsync(QueryMessage);
}

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

internal sealed class PassthroughBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest
{
    public ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

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