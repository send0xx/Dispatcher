using System.Collections.Immutable;
using Dispatcher.SourceGeneration.Models;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Analysis;

internal sealed class HandlerRouteSelector
{
    private readonly IReadOnlyDictionary<string, ImmutableArray<HandlerModel>> _handlersByMessageType;
    private readonly INamedTypeSymbol _query;
    private readonly INamedTypeSymbol _command;
    private readonly INamedTypeSymbol _resultlessCommand;
    private readonly INamedTypeSymbol _notification;

    internal HandlerRouteSelector(Compilation compilation, IEnumerable<HandlerModel> handlers)
    {
        _handlersByMessageType = handlers
            .GroupBy(
                static handler => GetTypeKey(handler.MessageType),
                StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToImmutableArray(),
                StringComparer.Ordinal);
        _query = compilation.GetTypeByMetadataName("Dispatcher.IQuery`1")!;
        _command = compilation.GetTypeByMetadataName("Dispatcher.ICommand`1")!;
        _resultlessCommand = compilation.GetTypeByMetadataName("Dispatcher.ICommand")!;
        _notification = compilation.GetTypeByMetadataName("Dispatcher.INotification")!;
    }

    internal HandlerModel? Select(
        INamedTypeSymbol messageType,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var candidates = GetCandidates(messageType).ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var handledTypes = candidates
            .Select(static handler => handler.MessageType)
            .GroupBy(GetTypeKey, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
        var mostSpecificTypes = handledTypes
            .Where(candidate => !handledTypes.Any(other =>
                !SymbolEqualityComparer.Default.Equals(candidate, other) &&
                IsAssignableTo(other, candidate)))
            .ToArray();
        if (mostSpecificTypes.Length != 1)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.AmbiguousHandlerRoute,
                messageType.Locations.FirstOrDefault(),
                messageType.ToDisplayString(SymbolDisplayFormats.FullyQualified),
                string.Join(
                    ", ",
                    mostSpecificTypes
                        .Select(type => type.ToDisplayString(SymbolDisplayFormats.FullyQualified))
                        .OrderBy(static name => name, StringComparer.Ordinal))));
            return null;
        }

        return candidates.First(handler => SymbolEqualityComparer.Default.Equals(
            handler.MessageType,
            mostSpecificTypes[0]));
    }

    internal bool HasCompatibleHandler(INamedTypeSymbol messageType) =>
        GetCandidates(messageType).Any();

    private IEnumerable<HandlerModel> GetCandidates(INamedTypeSymbol messageType) =>
        GetAssignableTypes(messageType)
            .SelectMany(handledType => _handlersByMessageType.TryGetValue(
                    GetTypeKey(handledType),
                    out var handlers)
                ? handlers
                : [])
            .Where(handler => IsCompatible(messageType, handler));

    private bool IsCompatible(INamedTypeSymbol messageType, HandlerModel handler)
    {
        var isResultlessCommand = messageType.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface, _resultlessCommand));
        return handler.Kind switch
        {
            HandlerModelKind.Query => HasResponse(messageType, _query, handler.ResponseType),
            HandlerModelKind.CommandWithResponse =>
                !isResultlessCommand && HasResponse(messageType, _command, handler.ResponseType),
            HandlerModelKind.Command => isResultlessCommand,
            _ => messageType.AllInterfaces.Any(@interface =>
                SymbolEqualityComparer.Default.Equals(@interface, _notification))
        };
    }

    private static IEnumerable<ITypeSymbol> GetAssignableTypes(INamedTypeSymbol messageType)
    {
        yield return messageType;

        for (var baseType = messageType.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            yield return baseType;
        }

        foreach (var @interface in messageType.AllInterfaces)
        {
            yield return @interface;
        }
    }

    private static bool IsAssignableTo(ITypeSymbol type, ITypeSymbol candidateBaseType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, candidateBaseType))
        {
            return true;
        }

        return type is INamedTypeSymbol namedType &&
            GetAssignableTypes(namedType).Any(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate, candidateBaseType));
    }

    private static bool HasResponse(
        INamedTypeSymbol messageType,
        INamedTypeSymbol messageDefinition,
        ITypeSymbol? responseType) =>
        messageType.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, messageDefinition) &&
            SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], responseType));

    private static string GetTypeKey(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormats.FullyQualified);
}