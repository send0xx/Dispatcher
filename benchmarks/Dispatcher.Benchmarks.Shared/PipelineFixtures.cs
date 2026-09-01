using Dispatcher.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks.Shared;

public sealed record PipelineQuery(int Value) : IQuery<int>;

internal sealed class PipelineQueryHandler(PipelineState state) : IQueryHandler<PipelineQuery, int>
{
    public ValueTask<int> HandleAsync(PipelineQuery query, CancellationToken cancellationToken)
    {
        state.HandlerCalls++;
        state.HandlerToken = cancellationToken;
        return ValueTask.FromResult(query.Value);
    }
}

public sealed class PipelineState : IDisposable
{
    private readonly CancellationTokenSource _replacement = new();

    public bool ValidateOrder { get; set; }

    public List<int> Order { get; } = [];

    public int HandlerCalls { get; set; }

    public CancellationToken HandlerToken { get; set; }

    public CancellationToken ReplacementToken => _replacement.Token;

    public void Dispose() => _replacement.Dispose();
}

internal abstract class PipelineBehavior(PipelineState state, int order)
{
    public ValueTask<int> HandleAsync(
        PipelineQuery request,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        if (state.ValidateOrder)
        {
            state.Order.Add(order);
        }

        var result = next(state.ReplacementToken);
        if (state.ValidateOrder)
        {
            state.Order.Add(-order);
        }

        return result;
    }
}

internal sealed class PipelineBehavior01(PipelineState state)
    : PipelineBehavior(state, 1), IPipelineBehavior<PipelineQuery, int>;

internal sealed class PipelineBehavior02(PipelineState state)
    : PipelineBehavior(state, 2), IPipelineBehavior<PipelineQuery, int>;

internal sealed class PipelineBehavior03(PipelineState state)
    : PipelineBehavior(state, 3), IPipelineBehavior<PipelineQuery, int>;

internal sealed class PipelineBehavior04(PipelineState state)
    : PipelineBehavior(state, 4), IPipelineBehavior<PipelineQuery, int>;

internal sealed class PipelineBehavior05(PipelineState state)
    : PipelineBehavior(state, 5), IPipelineBehavior<PipelineQuery, int>;

internal sealed class PipelineBehavior06(PipelineState state)
    : PipelineBehavior(state, 6), IPipelineBehavior<PipelineQuery, int>;

internal sealed class PipelineBehavior07(PipelineState state)
    : PipelineBehavior(state, 7), IPipelineBehavior<PipelineQuery, int>;

internal sealed class PipelineBehavior08(PipelineState state)
    : PipelineBehavior(state, 8), IPipelineBehavior<PipelineQuery, int>;

internal sealed class PipelineBehavior09(PipelineState state)
    : PipelineBehavior(state, 9), IPipelineBehavior<PipelineQuery, int>;

internal sealed class PipelineBehavior10(PipelineState state)
    : PipelineBehavior(state, 10), IPipelineBehavior<PipelineQuery, int>;

public static class PipelineRegistration
{
    private static readonly Type[] BehaviorTypes =
    [
        typeof(PipelineBehavior01), typeof(PipelineBehavior02), typeof(PipelineBehavior03),
        typeof(PipelineBehavior04), typeof(PipelineBehavior05), typeof(PipelineBehavior06),
        typeof(PipelineBehavior07), typeof(PipelineBehavior08), typeof(PipelineBehavior09),
        typeof(PipelineBehavior10)
    ];

    public static void Add(IServiceCollection services, int behaviorCount)
    {
        services.AddQueryHandler<PipelineQuery, int, PipelineQueryHandler>();
        services.AddDispatcherMessage<PipelineQuery>();

        for (var index = 0; index < behaviorCount; index++)
        {
            services.Add(ServiceDescriptor.Scoped(
                typeof(IPipelineBehavior<PipelineQuery, int>), BehaviorTypes[index]));
        }
    }
}