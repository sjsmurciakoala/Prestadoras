namespace SIAD.Core.Utilities;

public static class AccountCodeFormatter
{
    public const string DefaultMask = "###-###-##";
    public const string DefaultSeparator = "-";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    public static string Format(string? value, string? mask = DefaultMask, string? separator = DefaultSeparator)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var levels = ParseMask(mask);
        if (levels.Count == 0)
        {
            return normalized;
        }

        var sep = string.IsNullOrWhiteSpace(separator) ? DefaultSeparator : separator;
        var parts = new List<string>();
        var index = 0;

        foreach (var length in levels)
        {
            if (index >= normalized.Length)
            {
                break;
            }

            var take = Math.Min(length, normalized.Length - index);
            parts.Add(normalized.Substring(index, take));
            index += take;
        }

        if (index < normalized.Length)
        {
            parts.Add(normalized[index..]);
        }

        return string.Join(sep, parts);
    }

    public static string FormatDisplay(string? code, string? description)
    {
        var formattedCode = Format(code);
        var cleanDescription = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();

        if (string.IsNullOrWhiteSpace(formattedCode))
        {
            return cleanDescription;
        }

        return string.IsNullOrWhiteSpace(cleanDescription)
            ? formattedCode
            : $"{formattedCode} - {cleanDescription}";
    }

    private static IReadOnlyList<int> ParseMask(string? mask)
    {
        if (string.IsNullOrWhiteSpace(mask))
        {
            return Array.Empty<int>();
        }

        var levels = new List<int>();
        var current = 0;

        foreach (var ch in mask)
        {
            if (ch is '#' or 'X' or 'x')
            {
                current++;
                continue;
            }

            if (current > 0)
            {
                levels.Add(current);
                current = 0;
            }
        }

        if (current > 0)
        {
            levels.Add(current);
        }

        return levels;
    }
}
