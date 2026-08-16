using Dispatcher.TestSupport.Contracts;

namespace Dispatcher.TestSupport.ConflictingHandlers;

/// <summary>
/// Handlers in this assembly conflict on purpose. They live in their own assembly so that a scan
/// that discovers them cannot affect the tests that scan the supported handler assemblies.
/// </summary>
public sealed class ConflictingHandlerAssemblyMarker;

public interface IAlphaQuery : IQuery<string>;

public interface IBetaQuery : IQuery<string>;

/// <summary>
/// A query whose two handled interfaces are equally specific, so no route can be selected.
/// </summary>
public sealed record AmbiguousScanQuery : IAlphaQuery, IBetaQuery;

internal sealed class AlphaQueryHandler : IQueryHandler<IAlphaQuery, string>
{
    public ValueTask<string> HandleAsync(IAlphaQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult("alpha");
}

internal sealed class BetaQueryHandler : IQueryHandler<IBetaQuery, string>
{
    public ValueTask<string> HandleAsync(IBetaQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult("beta");
}

/// <summary>
/// A second handler for a query the shared handler assembly already handles, so registering both
/// duplicates the route.
/// </summary>
public sealed class ConflictingSharedBaseQueryHandler : IQueryHandler<SharedBaseQuery, string>
{
    public ValueTask<string> HandleAsync(SharedBaseQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult("conflicting " + query.Value);
}