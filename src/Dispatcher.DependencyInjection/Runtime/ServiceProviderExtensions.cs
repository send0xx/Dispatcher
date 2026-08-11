namespace Dispatcher;

internal static class ServiceProviderExtensions
{
    public static T GetRequiredService<T>(this IServiceProvider serviceProvider)
    {
        return (T?)serviceProvider.GetService(typeof(T))
            ?? throw new InvalidOperationException($"Service '{typeof(T).FullName}' is not registered.");
    }

    public static IReadOnlyList<T> GetServices<T>(this IServiceProvider serviceProvider)
    {
        var services = (IEnumerable<T>?)serviceProvider.GetService(typeof(IEnumerable<T>));

        return services switch
        {
            null => [],
            IReadOnlyList<T> list => list,
            _ => services.ToArray()
        };
    }
}
