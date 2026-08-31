using System.Text;

namespace Dispatcher.SourceGeneration.Emission;

internal sealed class CodeWriter
{
    private const string IndentText = "    ";
    private readonly StringBuilder output = new();
    private int indent;

    internal void Line(string text = "")
    {
        if (text.Length > 0)
        {
            for (var index = 0; index < indent; index++)
            {
                output.Append(IndentText);
            }

            output.Append(text);
        }

        output.AppendLine();
    }

    internal void Lines(string text)
    {
        var start = 0;
        while (start <= text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                Line(text.Substring(start));
                return;
            }

            var length = end - start;
            if (length > 0 && text[end - 1] == '\r')
            {
                length--;
            }

            Line(text.Substring(start, length));
            start = end + 1;
        }
    }

    internal void Begin(string declaration)
    {
        if (declaration.Length > 0)
        {
            Line(declaration);
        }

        Line("{");
        indent++;
    }

    internal void End(string suffix = "")
    {
        if (indent == 0)
        {
            throw new InvalidOperationException("Cannot close a block before one has been opened.");
        }

        indent--;
        Line("}" + suffix);
    }

    public override string ToString() => output.ToString();
}