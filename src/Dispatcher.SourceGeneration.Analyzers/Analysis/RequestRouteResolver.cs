using System.Collections.Immutable;
using Dispatcher.SourceGeneration.Diagnostics;
using Dispatcher.SourceGeneration.Discovery;
using Dispatcher.SourceGeneration.Model;
using Dispatcher.SourceGeneration.Support;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Analysis;

internal sealed class RequestRouteResolver
{
    private readonly IReadOnlyDictionary<string, ImmutableArray<HandlerDefinition>> handlersByMessage;
    private readonly DispatcherSymbols symbols;

    internal RequestRouteResolver(IEnumerable<HandlerDefinition> handlers, DispatcherSymbols symbols)
    {
        handlersByMessage = handlers
            .GroupBy(static handler => CSharpNames.Type(handler.MessageType), StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToImmutableArray(),
                StringComparer.Ordinal);
        this.symbols = symbols;
    }

    internal HandlerDefinition? Resolve(
        INamedTypeSymbol concreteMessage,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var candidates = Candidates(concreteMessage).ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var handledTypes = candidates
            .Select(static handler => handler.MessageType)
            .GroupBy(CSharpNames.Type, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
        var mostSpecific = handledTypes
            .Where(candidate => !handledTypes.Any(other =>
                !SymbolEqualityComparer.Default.Equals(candidate, other) && IsAssignableTo(other, candidate)))
            .ToArray();
        if (mostSpecific.Length != 1)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticCatalog.AmbiguousRoute,
                concreteMessage.Locations.FirstOrDefault(),
                CSharpNames.Type(concreteMessage),
                string.Join(", ",
                    mostSpecific.Select(CSharpNames.Type).OrderBy(static name => name, StringComparer.Ordinal))));
            return null;
        }

        return candidates.First(handler =>
            SymbolEqualityComparer.Default.Equals(handler.MessageType, mostSpecific[0]));
    }

    internal bool CanRoute(INamedTypeSymbol message) => Candidates(message).Any();

    private IEnumerable<HandlerDefinition> Candidates(INamedTypeSymbol message) =>
        AssignableTypes(message)
            .SelectMany(type => handlersByMessage.TryGetValue(CSharpNames.Type(type), out var handlers) ? handlers : [])
            .Where(handler => IsCompatible(message, handler));

    private bool IsCompatible(INamedTypeSymbol message, HandlerDefinition handler)
    {
        var resultless = message.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface, symbols.ResultlessCommand));
        return handler.Kind switch
        {
            HandlerKind.Query => HasResponse(message, symbols.Query, handler.ResponseType),
            HandlerKind.CommandWithResponse => !resultless &&
                                               HasResponse(message, symbols.Command, handler.ResponseType),
            HandlerKind.Command => resultless,
            _ => message.AllInterfaces.Any(@interface =>
                SymbolEqualityComparer.Default.Equals(@interface, symbols.Notification))
        };
    }

    private static IEnumerable<ITypeSymbol> AssignableTypes(INamedTypeSymbol type)
    {
        yield return type;
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            yield return baseType;
        }

        foreach (var @interface in type.AllInterfaces)
        {
            yield return @interface;
        }
    }

    private static bool IsAssignableTo(ITypeSymbol type, ITypeSymbol candidateBase) =>
        SymbolEqualityComparer.Default.Equals(type, candidateBase) ||
        type is INamedTypeSymbol named && AssignableTypes(named).Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate, candidateBase));

    private static bool HasResponse(
        INamedTypeSymbol message,
        INamedTypeSymbol? contract,
        ITypeSymbol? response) =>
        contract is not null && message.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, contract) &&
            SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], response));
}