using System.Text.RegularExpressions;

namespace DotNetConf.RagServer.Services;

public static partial class QueryTextNormalizer
{
    private static readonly (string Source, string Target)[] CanonicalReplacements =
    [
        (".net", " dotnet "),
        ("dot net", " dotnet "),
        ("asp.net", " aspnet "),
        ("c#", " csharp "),
        ("c++", " cplusplus "),
        ("f#", " fsharp "),
        ("node.js", " nodejs "),
        ("node js", " nodejs "),
        ("ms sql", " mssql ")
    ];

    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "how",
        "in", "is", "it", "of", "on", "or", "that", "the", "to", "was", "what",
        "when", "where", "which", "who", "with"
    ];

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = $" {text.Trim()} ";

        foreach (var (source, target) in CanonicalReplacements)
        {
            normalized = normalized.Replace(source, target, StringComparison.OrdinalIgnoreCase);
        }

        return MultiWhitespace().Replace(normalized, " ").Trim();
    }

    public static IReadOnlyList<string> Tokenize(string text)
    {
        var normalized = Normalize(text).ToLowerInvariant();
        var tokens = TokenPattern().Matches(normalized)
            .Select(match => match.Value)
            .Where(token => token.Length > 1 && !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return tokens;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespace();

    [GeneratedRegex(@"[\p{L}\p{Nd}][\p{L}\p{Nd}\.-]*", RegexOptions.IgnoreCase)]
    private static partial Regex TokenPattern();
}
