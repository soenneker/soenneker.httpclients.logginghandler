using System;
using Soenneker.Utils.PooledStringBuilders;

namespace Soenneker.HttpClients.LoggingHandler;

internal static class BodyLogText
{
    public static string EscapeControls(string text)
    {
        int index = 0;
        while (index < text.Length && !NeedsEscape(text[index]))
            index++;

        if (index == text.Length)
            return text;

        var escaped = new PooledStringBuilder(stackalloc char[256]);
        try
        {
            escaped.Append(text.AsSpan(0, index));
            const string hex = "0123456789abcdef";
            for (; index < text.Length; index++)
            {
                char value = text[index];
                if (NeedsEscape(value))
                {
                    escaped.Append("\\u");
                    escaped.Append(hex[(value >> 12) & 15]);
                    escaped.Append(hex[(value >> 8) & 15]);
                    escaped.Append(hex[(value >> 4) & 15]);
                    escaped.Append(hex[value & 15]);
                }
                else
                    escaped.Append(value);
            }

            return escaped.ToString();
        }
        finally
        {
            escaped.Dispose();
        }
    }

    // Keep ordinary multiline logs readable, but display terminal controls such as BEL and ESC.
    private static bool NeedsEscape(char value) => char.IsControl(value) && value is not ('\r' or '\n' or '\t');
}
