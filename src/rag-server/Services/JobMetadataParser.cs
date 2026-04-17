using DotNetConf.Knowledge.Data;

namespace DotNetConf.RagServer.Services;

public static class JobMetadataParser
{
    public static IReadOnlyList<string> Split(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    public static string InferSeniority(JobChunkDocument document)
    {
        var tokens = QueryTextNormalizer.Tokenize($"{document.Title} {document.Tags} {document.ChunkText}");
        var tokenSet = tokens.ToHashSet(StringComparer.Ordinal);

        if (ContainsAny(tokenSet, "junior", "entry", "intern", "graduate", "trainee"))
        {
            return "junior";
        }

        if (ContainsAny(tokenSet, "senior", "lead", "principal", "staff", "architect"))
        {
            return "senior";
        }

        if (tokenSet.Contains("notjunior"))
        {
            return "mid-to-senior";
        }

        if (ContainsAny(tokenSet, "mid", "middle", "regular", "intermediate", "experienced"))
        {
            return "regular";
        }

        return string.Empty;
    }

    private static bool ContainsAny(IReadOnlySet<string> tokens, params string[] values)
    {
        return values.Any(tokens.Contains);
    }
}
