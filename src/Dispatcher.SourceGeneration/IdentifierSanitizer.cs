using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Dispatcher.SourceGeneration;

internal static class IdentifierSanitizer
{
    internal static string SanitizeNamespace(string value)
    {
        var segments = value.Split('.');
        for (var index = 0; index < segments.Length; index++)
        {
            segments[index] = SanitizeIdentifier(segments[index]);
        }

        return string.Join(".", segments);
    }

    internal static string SanitizeIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length + 1);
        if (value.Length == 0 || !SyntaxFacts.IsIdentifierStartCharacter(value[0]))
        {
            builder.Append('_');
        }

        foreach (var character in value)
        {
            builder.Append(SyntaxFacts.IsIdentifierPartCharacter(character) ? character : '_');
        }

        var identifier = builder.ToString();
        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
                ? "_" + identifier
                : identifier;
    }
}