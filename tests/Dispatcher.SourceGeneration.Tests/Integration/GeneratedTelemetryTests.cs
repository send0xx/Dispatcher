using System.Collections.Concurrent;
using System.Diagnostics;
using Dispatcher.SourceGeneration.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.SourceGeneration.Tests.Integration;

public sealed class GeneratedTelemetryTests
{
    [Fact]
    public async Task Async_dispatch_restores_the_parent_activity()
    {
        var instrumentationName = "Dispatcher.SourceGeneration.Tests." + Guid.NewGuid();
        using var capture = new GeneratedActivityCapture(instrumentationName);
        var services = new ServiceCollection();
        services.AddSingleton<GeneratedTestState>();
        services
            .AddGeneratedIntegrationHandlers()
            .AddGeneratedIntegrationDispatcher(options =>
            {
                options.Telemetry.EnableTracing = true;
                options.Telemetry.ActivitySourceName = instrumentationName;
            });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var state = scope.ServiceProvider.GetRequiredService<GeneratedTestState>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        using var parent = new Activity("parent").Start();

        var response = dispatcher.QueryAsync(new GeneratedDelayedQuery(), TestContext.Current.CancellationToken);

        Assert.False(response.IsCompleted);
        Assert.Same(parent, Activity.Current);

        state.Completion.SetResult("completed");

        Assert.Equal("completed", await response);
        Assert.Same(parent, Activity.Current);
        Assert.Same(parent, Assert.Single(capture.Activities).Parent);
    }

    private sealed class GeneratedActivityCapture : IDisposable
    {
        private readonly ActivityListener _listener;

        internal GeneratedActivityCapture(string activitySourceName)
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == activitySourceName,
                Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => Activities.Enqueue(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        internal ConcurrentQueue<Activity> Activities { get; } = new();

        public void Dispose() => _listener.Dispose();
    }
}