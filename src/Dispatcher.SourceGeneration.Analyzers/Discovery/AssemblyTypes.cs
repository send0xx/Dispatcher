using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Discovery;

internal static class AssemblyTypes
{
    internal static IEnumerable<INamedTypeSymbol> Enumerate(IAssemblySymbol assembly) =>
        Enumerate(assembly.GlobalNamespace);

    internal static bool HasGeneratedHandlerRegistration(IAssemblySymbol assembly) =>
        assembly.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() ==
            "Dispatcher.SourceGeneration.GenerateDispatcherHandlersAttribute");

    internal static bool HasPublicConstructor(INamedTypeSymbol type) =>
        type.InstanceConstructors.Any(static constructor =>
            !constructor.IsStatic && constructor.DeclaredAccessibility == Accessibility.Public);

    internal static bool IsNestedInGenericType(INamedTypeSymbol type)
    {
        for (var containing = type.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            if (containing.Arity != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> Enumerate(INamespaceSymbol @namespace)
    {
        foreach (var type in @namespace.GetTypeMembers())
        {
            foreach (var nested in Enumerate(type))
            {
                yield return nested;
            }
        }

        foreach (var child in @namespace.GetNamespaceMembers())
        {
            foreach (var type in Enumerate(child))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> Enumerate(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers())
        {
            foreach (var descendant in Enumerate(nested))
            {
                yield return descendant;
            }
        }
    }
}