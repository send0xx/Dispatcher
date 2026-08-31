using Dispatcher.SourceGeneration.Model;
using Dispatcher.SourceGeneration.Support;

namespace Dispatcher.SourceGeneration.Emission;

internal sealed class OpenHandlerMethodNames
{
    internal OpenHandlerMethodNames(OpenNotificationHandlerDefinition handler)
    {
        var implementation = CSharpNames.Type(handler.ImplementationType);
        Suffix = GeneratedIdentifier.From(implementation) + "_" + StableHash(implementation).ToString("X8");
        RegistrationClass = "global::Dispatcher.SourceGeneration.GeneratedHandlerServiceCollectionExtensions_" +
            GeneratedIdentifier.From(handler.ImplementationType.ContainingAssembly.Name);
    }

    internal string Suffix { get; }
    internal string RegistrationClass { get; }

    private static uint StableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }
}