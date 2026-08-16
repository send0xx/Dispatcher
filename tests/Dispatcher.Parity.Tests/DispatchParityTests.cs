using Dispatcher.Parity.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.Parity.Tests;

/// <summary>
/// The dispatch behavior both implementations must share, written once and run against each.
/// </summary>
/// <remarks>
/// Every scenario dispatches the same CLR types through both the reflection-based and the
/// source-generated dispatcher, so a behavior that changes in one implementation and not the other
/// fails here instead of drifting unnoticed. Registration mechanics deliberately stay out of scope:
/// assembly scanning, runtime <see cref="MessageRegistration"/> metadata, and registration order have
/// no source-generated equivalent, and each implementation covers those on its own. A request with no
/// handler is out of scope for the same reason: the generator rejects it at compile time, so it cannot
/// be declared in this assembly.
/// </remarks>
public abstract class DispatchParityTests
{
    /// <summary>
    /// Registers the implementation under test, together with the parity handlers and behaviors.
    /// </summary>
    private protected abstract void Register(IServiceCollection services);

    [Fact]
    public async Task Dispatches_a_query_through_its_pipeline_behavior()
    {
        await using var host = CreateHost();

        var response = await host.Dispatcher.QueryAsync(new GreetQuery("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal("Hello, Ada", response);
        Assert.Equal(["greet-before", "greet", "greet-after"], host.Recorder.Events);
    }

    [Fact]
    public async Task Runs_the_first_registered_behavior_outermost()
    {
        await using var host = CreateHost();

        await host.Dispatcher.QueryAsync(new OrderedQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["first-before", "second-before", "ordered", "second-after", "first-after"],
            host.Recorder.Events);
    }

    [Fact]
    public async Task Behavior_can_short_circuit_the_handler()
    {
        await using var host = CreateHost();

        var response = await host.Dispatcher.QueryAsync(new CachedQuery(), TestContext.Current.CancellationToken);

        Assert.Equal("cached", response);
        Assert.Empty(host.Recorder.Events);
    }

    [Fact]
    public async Task Resultless_command_runs_through_a_unit_behavior()
    {
        await using var host = CreateHost();

        await host.Dispatcher.ExecuteAsync(new TrackedCommand("value"), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["tracked-before", "tracked-value", "tracked-after-True"],
            host.Recorder.Events);
    }

    [Fact]
    public async Task Dispatches_a_command_that_returns_a_response()
    {
        await using var host = CreateHost();

        var response = await host.Dispatcher.ExecuteAsync(new SumCommand(2, 3), TestContext.Current.CancellationToken);

        Assert.Equal(5, response);
        Assert.Equal(["sum"], host.Recorder.Events);
    }

    [Fact]
    public async Task Dispatches_a_resultless_command()
    {
        await using var host = CreateHost();

        await host.Dispatcher.ExecuteAsync(new RecordCommand("value"), TestContext.Current.CancellationToken);

        Assert.Equal(["record-value"], host.Recorder.Events);
    }

    [Fact]
    public async Task Routes_a_derived_query_to_the_base_handler()
    {
        await using var host = CreateHost();

        var response = await host.Dispatcher.QueryAsync(
            new DailyReportQuery("daily"),
            TestContext.Current.CancellationToken);

        Assert.Equal("base daily", response);
    }

    [Fact]
    public async Task Exact_query_handler_suppresses_the_base_handler()
    {
        await using var host = CreateHost();

        var response = await host.Dispatcher.QueryAsync(
            new HourlyReportQuery("hourly"),
            TestContext.Current.CancellationToken);

        Assert.Equal("exact hourly", response);
    }

    [Fact]
    public async Task Routes_a_derived_notification_to_every_base_handler_in_registration_order()
    {
        await using var host = CreateHost();

        await host.Dispatcher.PublishAsync(new UserUpdated(), TestContext.Current.CancellationToken);

        Assert.Equal(["domain-a", "domain-b", "audit-UserUpdated"], host.Recorder.Events);
    }

    [Fact]
    public async Task Exact_notification_handler_suppresses_base_handlers_while_open_handlers_still_run()
    {
        await using var host = CreateHost();

        await host.Dispatcher.PublishAsync(new UserCreated(), TestContext.Current.CancellationToken);

        Assert.Equal(["user-created", "audit-UserCreated"], host.Recorder.Events);
    }

    [Fact]
    public async Task Invokes_closed_notification_handlers_sequentially_in_registration_order()
    {
        await using var host = CreateHost();

        await host.Dispatcher.PublishAsync(new Heartbeat(), TestContext.Current.CancellationToken);

        Assert.Equal(["heartbeat-a", "heartbeat-b"], host.Recorder.Events);
    }

    [Fact]
    public async Task Publishing_a_notification_without_handlers_does_nothing()
    {
        await using var host = CreateHost();

        await host.Dispatcher.PublishAsync(new Ignored(), TestContext.Current.CancellationToken);

        Assert.Empty(host.Recorder.Events);
    }

    [Fact]
    public async Task Focused_dispatcher_contracts_resolve_to_the_same_implementation()
    {
        await using var host = CreateHost();
        var provider = host.Scope.ServiceProvider;

        Assert.Same(host.Dispatcher, provider.GetRequiredService<IQueryDispatcher>());
        Assert.Same(host.Dispatcher, provider.GetRequiredService<ICommandDispatcher>());
        Assert.Same(host.Dispatcher, provider.GetRequiredService<INotificationDispatcher>());
    }

    [Fact]
    public async Task Passes_the_cancellation_token_through_to_the_handler()
    {
        await using var host = CreateHost();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await host.Dispatcher.QueryAsync(new CancellationQuery(), cancellation.Token));
    }

    private ParityHost CreateHost()
    {
        var services = new ServiceCollection();
        services.AddScoped<ParityRecorder>();
        Register(services);

        return new ParityHost(services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        }));
    }

    private sealed class ParityHost(ServiceProvider provider) : IAsyncDisposable
    {
        internal AsyncServiceScope Scope { get; } = provider.CreateAsyncScope();

        internal IDispatcher Dispatcher => Scope.ServiceProvider.GetRequiredService<IDispatcher>();

        internal ParityRecorder Recorder => Scope.ServiceProvider.GetRequiredService<ParityRecorder>();

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await provider.DisposeAsync();
        }
    }
}