using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Dispatcher.SourceGeneration.Support;

internal static class GeneratedIdentifier
{
    internal static string From(string value)
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