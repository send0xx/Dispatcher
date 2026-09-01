using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using Dispatcher.DependencyInjection;
using Dispatcher.SourceGeneration;
using Microsoft.Extensions.DependencyInjection;

[assembly: GenerateDispatcherHandlers("AddGeneratedBenchmarkHandlers")]

namespace Dispatcher.Benchmarks.Shared;

public enum BenchmarkImplementation
{
    Reflection,
    SourceGenerated
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class DispatcherBenchmarkAttribute : Attribute, IConfigSource
{
    public IConfig Config { get; } = ManualConfig.CreateEmpty()
        .AddDiagnoser(MemoryDiagnoser.Default)
        .HideColumns("Error", "StdDev", "RatioSD");
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class ScaleOperationBenchmarkAttribute : Attribute, IConfigSource
{
    public IConfig Config { get; } = ManualConfig.CreateEmpty()
        .AddDiagnoser(MemoryDiagnoser.Default)
        .HideColumns("Error", "StdDev", "RatioSD")
        .AddJob(Job.Default
            .WithEnvironmentVariable("DOTNET_TieredCompilation", "0")
            .WithInvocationCount(1)
            .WithUnrollFactor(1));
}

public sealed class BenchmarkProvider : IDisposable
{
    private readonly ServiceProvider _provider;

    private BenchmarkProvider(ServiceProvider provider) => _provider = provider;

    public static BenchmarkProvider Create(
        BenchmarkImplementation implementation,
        Action<IServiceCollection, Action<DispatcherOptions>> addGeneratedDispatcher,
        Action<IServiceCollection>? configureHandlers = null,
        Action<DispatcherOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        services.AddScoped<PipelineState>();
        services.AddScoped<FanOutState>();

        if (configureHandlers is null)
        {
            if (implementation == BenchmarkImplementation.Reflection)
            {
                services.AddDispatcherHandlers(typeof(BenchmarkProvider).Assembly);
            }
            else
            {
                services.AddGeneratedBenchmarkHandlers();
            }
        }
        else
        {
            configureHandlers(services);
        }

        if (implementation == BenchmarkImplementation.Reflection)
        {
            services.AddDispatcher(options => configureOptions?.Invoke(options));
        }
        else
        {
            addGeneratedDispatcher(services, options => configureOptions?.Invoke(options));
        }

        return new BenchmarkProvider(services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        }));
    }

    public IServiceScope CreateScope() => _provider.CreateScope();

    public BenchmarkHost CreateHost() => new(this, CreateScope());

    public void Dispose() => _provider.Dispose();
}

public sealed class BenchmarkHost : IDisposable
{
    private readonly IServiceScope _scope;

    internal BenchmarkHost(BenchmarkProvider owner, IServiceScope scope)
    {
        _ = owner;
        _scope = scope;
        Dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
    }

    public IDispatcher Dispatcher { get; }

    public IServiceProvider Services => _scope.ServiceProvider;

    public void Dispose() => _scope.Dispose();
}

public sealed class ScopedProbe
{
    public Guid Id { get; } = Guid.NewGuid();
}

public static class BenchmarkProfiles
{
    public static string[] Select(string[] args, IReadOnlyDictionary<string, string[]> profiles)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            return args;
        }

        if (!profiles.TryGetValue(args[0], out var filters))
        {
            return args;
        }

        return ["--filter", .. filters, .. args.Skip(1)];
    }
}