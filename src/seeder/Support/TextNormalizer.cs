using System.Net;
using System.Text.RegularExpressions;

namespace DotNetConf.Seeder.Support;

public static partial class TextNormalizer
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(value);
        var normalized = WhitespaceRegex().Replace(decoded, " ").Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
