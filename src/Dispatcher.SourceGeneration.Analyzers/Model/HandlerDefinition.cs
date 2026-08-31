using Dispatcher.SourceGeneration.Support;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Model;

internal sealed class HandlerDefinition(
    HandlerKind kind,
    ITypeSymbol messageType,
    ITypeSymbol? responseType,
    INamedTypeSymbol implementationType)
{
    internal HandlerKind Kind { get; } = kind;
    internal ITypeSymbol MessageType { get; } = messageType;
    internal ITypeSymbol? ResponseType { get; } = responseType;
    internal INamedTypeSymbol ImplementationType { get; } = implementationType;
    internal string SortKey { get; } = CSharpNames.Type(messageType) + "|" + CSharpNames.Type(implementationType);
}