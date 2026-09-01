namespace Dispatcher.Benchmarks.Scale;

public enum FixtureSize
{
    Small,
    Medium,
    Large
}

internal sealed record FixtureConfiguration(
    FixtureSize Size,
    int ModuleCount,
    int MessageCount,
    int Seed)
{
    internal static FixtureConfiguration Create(FixtureSize size) => size switch
    {
        FixtureSize.Small => new(size, 1, 100, 1729),
        FixtureSize.Medium => new(size, 8, 1_000, 1729),
        FixtureSize.Large => new(size, 32, 5_000, 1729),
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
}

internal sealed record FixtureSources(string Contracts, string[] Modules, string Host)
{
    internal IEnumerable<string> All
    {
        get
        {
            yield return Contracts;
            foreach (var module in Modules)
            {
                yield return module;
            }

            yield return Host;
        }
    }
}