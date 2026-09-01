using Dispatcher.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks.Shared;

public sealed record FanOutNotification : INotification;

public sealed class FanOutState
{
    public List<int> HandlerOrder { get; } = [];

    public bool ValidateOrder { get; set; }
}

internal abstract class FanOutHandler(FanOutState state, int order)
{
    public ValueTask HandleAsync(FanOutNotification notification, CancellationToken cancellationToken)
    {
        if (state.ValidateOrder)
        {
            state.HandlerOrder.Add(order);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class FanOutHandler01(FanOutState state)
    : FanOutHandler(state, 1), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler02(FanOutState state)
    : FanOutHandler(state, 2), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler03(FanOutState state)
    : FanOutHandler(state, 3), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler04(FanOutState state)
    : FanOutHandler(state, 4), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler05(FanOutState state)
    : FanOutHandler(state, 5), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler06(FanOutState state)
    : FanOutHandler(state, 6), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler07(FanOutState state)
    : FanOutHandler(state, 7), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler08(FanOutState state)
    : FanOutHandler(state, 8), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler09(FanOutState state)
    : FanOutHandler(state, 9), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler10(FanOutState state)
    : FanOutHandler(state, 10), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler11(FanOutState state)
    : FanOutHandler(state, 11), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler12(FanOutState state)
    : FanOutHandler(state, 12), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler13(FanOutState state)
    : FanOutHandler(state, 13), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler14(FanOutState state)
    : FanOutHandler(state, 14), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler15(FanOutState state)
    : FanOutHandler(state, 15), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler16(FanOutState state)
    : FanOutHandler(state, 16), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler17(FanOutState state)
    : FanOutHandler(state, 17), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler18(FanOutState state)
    : FanOutHandler(state, 18), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler19(FanOutState state)
    : FanOutHandler(state, 19), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler20(FanOutState state)
    : FanOutHandler(state, 20), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler21(FanOutState state)
    : FanOutHandler(state, 21), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler22(FanOutState state)
    : FanOutHandler(state, 22), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler23(FanOutState state)
    : FanOutHandler(state, 23), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler24(FanOutState state)
    : FanOutHandler(state, 24), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler25(FanOutState state)
    : FanOutHandler(state, 25), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler26(FanOutState state)
    : FanOutHandler(state, 26), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler27(FanOutState state)
    : FanOutHandler(state, 27), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler28(FanOutState state)
    : FanOutHandler(state, 28), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler29(FanOutState state)
    : FanOutHandler(state, 29), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler30(FanOutState state)
    : FanOutHandler(state, 30), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler31(FanOutState state)
    : FanOutHandler(state, 31), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler32(FanOutState state)
    : FanOutHandler(state, 32), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler33(FanOutState state)
    : FanOutHandler(state, 33), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler34(FanOutState state)
    : FanOutHandler(state, 34), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler35(FanOutState state)
    : FanOutHandler(state, 35), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler36(FanOutState state)
    : FanOutHandler(state, 36), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler37(FanOutState state)
    : FanOutHandler(state, 37), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler38(FanOutState state)
    : FanOutHandler(state, 38), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler39(FanOutState state)
    : FanOutHandler(state, 39), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler40(FanOutState state)
    : FanOutHandler(state, 40), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler41(FanOutState state)
    : FanOutHandler(state, 41), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler42(FanOutState state)
    : FanOutHandler(state, 42), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler43(FanOutState state)
    : FanOutHandler(state, 43), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler44(FanOutState state)
    : FanOutHandler(state, 44), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler45(FanOutState state)
    : FanOutHandler(state, 45), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler46(FanOutState state)
    : FanOutHandler(state, 46), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler47(FanOutState state)
    : FanOutHandler(state, 47), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler48(FanOutState state)
    : FanOutHandler(state, 48), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler49(FanOutState state)
    : FanOutHandler(state, 49), INotificationHandler<FanOutNotification>;

internal sealed class FanOutHandler50(FanOutState state)
    : FanOutHandler(state, 50), INotificationHandler<FanOutNotification>;

public static class FanOutRegistration
{
    private static readonly Type[] HandlerTypes =
    [
        typeof(FanOutHandler01), typeof(FanOutHandler02), typeof(FanOutHandler03),
        typeof(FanOutHandler04), typeof(FanOutHandler05), typeof(FanOutHandler06),
        typeof(FanOutHandler07), typeof(FanOutHandler08), typeof(FanOutHandler09),
        typeof(FanOutHandler10), typeof(FanOutHandler11), typeof(FanOutHandler12),
        typeof(FanOutHandler13), typeof(FanOutHandler14), typeof(FanOutHandler15),
        typeof(FanOutHandler16), typeof(FanOutHandler17), typeof(FanOutHandler18),
        typeof(FanOutHandler19), typeof(FanOutHandler20), typeof(FanOutHandler21),
        typeof(FanOutHandler22), typeof(FanOutHandler23), typeof(FanOutHandler24),
        typeof(FanOutHandler25), typeof(FanOutHandler26), typeof(FanOutHandler27),
        typeof(FanOutHandler28), typeof(FanOutHandler29), typeof(FanOutHandler30),
        typeof(FanOutHandler31), typeof(FanOutHandler32), typeof(FanOutHandler33),
        typeof(FanOutHandler34), typeof(FanOutHandler35), typeof(FanOutHandler36),
        typeof(FanOutHandler37), typeof(FanOutHandler38), typeof(FanOutHandler39),
        typeof(FanOutHandler40), typeof(FanOutHandler41), typeof(FanOutHandler42),
        typeof(FanOutHandler43), typeof(FanOutHandler44), typeof(FanOutHandler45),
        typeof(FanOutHandler46), typeof(FanOutHandler47), typeof(FanOutHandler48),
        typeof(FanOutHandler49), typeof(FanOutHandler50)
    ];

    public static void Add(IServiceCollection services, int handlerCount)
    {
        services.AddDispatcherMessage<FanOutNotification>();
        for (var index = 0; index < handlerCount; index++)
        {
            services.Add(ServiceDescriptor.Scoped(
                typeof(INotificationHandler<FanOutNotification>), HandlerTypes[index]));
        }
    }
}