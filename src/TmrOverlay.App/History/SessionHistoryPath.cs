using System.Text;

namespace TmrOverlay.App.History;

internal static class SessionHistoryPath
{
    public static string Slug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        return builder.ToString().Trim('-');
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        return (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9');
    }
}
