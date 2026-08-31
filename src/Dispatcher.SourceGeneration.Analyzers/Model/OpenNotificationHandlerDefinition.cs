using Dispatcher.SourceGeneration.Support;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Model;

internal sealed class OpenNotificationHandlerDefinition(INamedTypeSymbol implementationType)
{
    internal INamedTypeSymbol ImplementationType { get; } = implementationType;
    internal ITypeParameterSymbol NotificationTypeParameter { get; } = implementationType.TypeParameters[0];
    internal string SortKey { get; } = CSharpNames.Type(implementationType);

    internal bool IsCallableFromOtherAssemblies =>
        NotificationTypeParameter.ConstraintTypes.All(IsPubliclyAccessible);

    private static bool IsPubliclyAccessible(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsPubliclyAccessible(array.ElementType),
        IPointerTypeSymbol pointer => IsPubliclyAccessible(pointer.PointedAtType),
        INamedTypeSymbol named =>
            named.DeclaredAccessibility == Accessibility.Public &&
            (named.ContainingType is null || IsPubliclyAccessible(named.ContainingType)) &&
            named.TypeArguments.All(IsPubliclyAccessible),
        _ => true
    };
}