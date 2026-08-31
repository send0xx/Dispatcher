using System.Collections.Immutable;
using Dispatcher.SourceGeneration.Diagnostics;
using Dispatcher.SourceGeneration.Support;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Discovery;

internal static class PipelineBehaviorScanner
{
    internal static ImmutableArray<INamedTypeSymbol> Scan(
        ImmutableArray<INamedTypeSymbol> types,
        INamedTypeSymbol? behaviorContract,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (behaviorContract is null)
        {
            return [];
        }

        var result = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var type in types.Where(static type =>
                     type.TypeKind == TypeKind.Class && !type.IsAbstract && type.Arity > 0))
        {
            var contract = type.AllInterfaces.FirstOrDefault(@interface =>
                SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, behaviorContract));
            if (contract is null)
            {
                continue;
            }

            var supported = type.Arity == 2 &&
                SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], type.TypeParameters[0]) &&
                SymbolEqualityComparer.Default.Equals(contract.TypeArguments[1], type.TypeParameters[1]) &&
                AssemblyTypes.HasPublicConstructor(type);
            if (supported)
            {
                result.Add(type);
            }
            else
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticCatalog.UnsupportedPipelineBehavior,
                    type.Locations.FirstOrDefault(),
                    CSharpNames.Type(type)));
            }
        }

        return result.OrderBy(CSharpNames.Type, StringComparer.Ordinal).ToImmutableArray();
    }
}