using System.Collections.Immutable;
using Dispatcher.SourceGeneration.Diagnostics;
using Dispatcher.SourceGeneration.Discovery;
using Dispatcher.SourceGeneration.Model;
using Dispatcher.SourceGeneration.Support;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Analysis;

internal static class HandlerValidation
{
    internal static void ReportDuplicates(
        IEnumerable<HandlerDefinition> handlers,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (var group in handlers
                     .Where(static handler => handler.Kind != HandlerKind.Notification)
                     .GroupBy(static handler => handler.MessageType, SymbolEqualityComparer.Default)
                     .Where(static group => group.Count() > 1))
        {
            var implementations = string.Join(
                ", ",
                group.Select(static handler => CSharpNames.Type(handler.ImplementationType))
                    .OrderBy(static name => name, StringComparer.Ordinal));
            diagnostics.Add(Diagnostic.Create(
                DiagnosticCatalog.DuplicateRequestHandler,
                group.Key!.Locations.FirstOrDefault(),
                CSharpNames.Type((ITypeSymbol)group.Key),
                implementations));
        }
    }

    internal static void ReportMissingRequests(
        RequestRouteResolver resolver,
        ImmutableArray<INamedTypeSymbol> localTypes,
        DispatcherSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (symbols.Query is null || symbols.Command is null || symbols.ResultlessCommand is null)
        {
            return;
        }

        foreach (var type in localTypes.Where(type =>
                     type.Locations.Any(static location => location.IsInSource) &&
                     type.Arity == 0 &&
                     !AssemblyTypes.IsNestedInGenericType(type) &&
                     !type.IsAbstract && IsRequest(type, symbols)))
        {
            if (!resolver.CanRoute(type))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticCatalog.MissingRequestHandler,
                    type.Locations.FirstOrDefault(),
                    CSharpNames.Type(type)));
            }
        }
    }

    private static bool IsRequest(INamedTypeSymbol type, DispatcherSymbols symbols) =>
        type.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, symbols.Query) ||
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, symbols.Command) ||
            SymbolEqualityComparer.Default.Equals(@interface, symbols.ResultlessCommand));
}