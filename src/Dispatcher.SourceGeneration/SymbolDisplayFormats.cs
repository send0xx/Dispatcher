using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration;

internal static class SymbolDisplayFormats
{
    internal static readonly SymbolDisplayFormat FullyQualified =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
}
