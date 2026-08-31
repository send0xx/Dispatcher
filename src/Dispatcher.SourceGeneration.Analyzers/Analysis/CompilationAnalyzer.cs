using System.Collections.Immutable;
using Dispatcher.SourceGeneration.Diagnostics;
using Dispatcher.SourceGeneration.Discovery;
using Dispatcher.SourceGeneration.Model;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Analysis;

internal static class CompilationAnalyzer
{
    internal static GenerationModel Analyze(Compilation compilation, CancellationToken cancellationToken)
    {
        var settings = GenerationSettings.Read(compilation, cancellationToken);
        if (settings is null)
        {
            return GenerationModel.Empty;
        }

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        diagnostics.AddRange(settings.Diagnostics);
        if (settings.HasErrors)
        {
            return Empty(settings, diagnostics.ToImmutable());
        }

        var symbols = DispatcherSymbols.Resolve(compilation);
        if (symbols is null)
        {
            return Empty(settings, diagnostics.ToImmutable());
        }

        var localTypes = AssemblyTypes.Enumerate(compilation.Assembly).ToImmutableArray();
        var localHandlers = HandlerScanner.ScanCurrentAssembly(
            compilation,
            symbols,
            localTypes,
            diagnostics,
            cancellationToken);
        var behaviors = PipelineBehaviorScanner.Scan(localTypes, symbols.PipelineBehavior, diagnostics);
        ReportUnregisteredLocalHandlers(settings, compilation, localHandlers, diagnostics, cancellationToken);

        var dispatchHandlers = ImmutableArray.CreateBuilder<HandlerDefinition>();
        dispatchHandlers.AddRange(localHandlers.ClosedHandlers);
        var dispatchOpenHandlers = ImmutableArray.CreateBuilder<OpenNotificationHandlerDefinition>();
        dispatchOpenHandlers.AddRange(localHandlers.OpenNotificationHandlers);
        if (settings.DispatcherAttribute is not null)
        {
            var referenced = HandlerScanner.ScanReferencedModules(
                compilation,
                symbols,
                diagnostics,
                cancellationToken);
            dispatchHandlers.AddRange(referenced.ClosedHandlers);
            dispatchOpenHandlers.AddRange(referenced.OpenNotificationHandlers);
        }

        var orderedHandlers = dispatchHandlers
            .OrderBy(static handler => handler.SortKey, StringComparer.Ordinal)
            .ToImmutableArray();
        var orderedOpenHandlers = dispatchOpenHandlers
            .OrderBy(static handler => handler.SortKey, StringComparer.Ordinal)
            .ToImmutableArray();
        HandlerValidation.ReportDuplicates(
            settings.DispatcherAttribute is null ? localHandlers.ClosedHandlers : orderedHandlers,
            diagnostics);

        var resolver = CanResolveRoutes(symbols) ? new RequestRouteResolver(orderedHandlers, symbols) : null;
        var routes = settings.DispatcherAttribute is not null && resolver is not null
            ? DispatchRouteDiscovery.Discover(
                compilation,
                symbols,
                localTypes,
                orderedHandlers,
                orderedOpenHandlers,
                resolver,
                diagnostics,
                cancellationToken)
            : ImmutableArray<DispatchRoute>.Empty;
        if (resolver is not null)
        {
            HandlerValidation.ReportMissingRequests(resolver, localTypes, symbols, diagnostics);
        }

        return new GenerationModel(
            settings.HandlerMethod,
            settings.DispatcherMethod,
            localHandlers.ClosedHandlers,
            orderedHandlers,
            localHandlers.OpenNotificationHandlers,
            orderedOpenHandlers,
            routes,
            behaviors,
            diagnostics.ToImmutable(),
            compilation.AssemblyName ?? "DispatcherModule",
            symbols.Unit);
    }

    private static bool CanResolveRoutes(DispatcherSymbols symbols) =>
        symbols.Query is not null && symbols.Command is not null && symbols.ResultlessCommand is not null &&
        symbols.Notification is not null;

    private static void ReportUnregisteredLocalHandlers(
        GenerationSettings settings,
        Compilation compilation,
        HandlerInventory localHandlers,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (settings.DispatcherAttribute is null || settings.HandlerAttribute is not null ||
            localHandlers.ClosedHandlers.IsDefaultOrEmpty && localHandlers.OpenNotificationHandlers.IsDefaultOrEmpty)
        {
            return;
        }

        diagnostics.Add(Diagnostic.Create(
            DiagnosticCatalog.UnregisteredLocalHandlers,
            settings.DispatcherAttribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation(),
            compilation.Assembly.Name));
    }

    private static GenerationModel Empty(GenerationSettings settings, ImmutableArray<Diagnostic> diagnostics) =>
        new(
            settings.HandlerMethod,
            settings.DispatcherMethod,
            [],
            [],
            [],
            [],
            [],
            [],
            diagnostics);
}