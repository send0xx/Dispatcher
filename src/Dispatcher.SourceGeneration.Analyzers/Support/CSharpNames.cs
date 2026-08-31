using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Support;

internal static class CSharpNames
{
    private static readonly SymbolDisplayFormat Format =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    internal static string Type(ITypeSymbol symbol) => symbol.ToDisplayString(Format);
}