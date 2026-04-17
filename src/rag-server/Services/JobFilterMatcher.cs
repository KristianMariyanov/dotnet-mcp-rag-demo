using DotNetConf.Knowledge.Data;
using DotNetConf.RagServer.Contracts;

namespace DotNetConf.RagServer.Services;

public sealed class JobFilterMatcher
{
    private static readonly IReadOnlyDictionary<string, string[]> SeniorityAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["junior"] = ["junior", "entry", "intern", "graduate", "trainee"],
            ["regular"] = ["regular", "mid", "middle", "intermediate", "experienced", "notjunior"],
            ["mid"] = ["regular", "mid", "middle", "intermediate", "experienced", "notjunior"],
            ["senior"] = ["senior", "lead", "principal", "staff", "architect", "expert", "notjunior"]
        };

    public FilterEvaluation Evaluate(JobChunkDocument document, RetrievalFilters filters)
    {
        if (IsEmpty(filters))
        {
            return FilterEvaluation.Match([]);
        }

        var matchedFilters = new List<string>();

        if (!MatchesRequestedValues(
                filters.Technologies,
                [.. JobMetadataParser.Split(document.Technologies), .. JobMetadataParser.Split(document.Tags), document.Title],
                "technology",
                matchedFilters))
        {
            return FilterEvaluation.NoMatch;
        }

        if (!MatchesRequestedValues(
                filters.Categories,
                [.. JobMetadataParser.Split(document.Categories), .. JobMetadataParser.Split(document.Tags), document.Title],
                "category",
                matchedFilters))
        {
            return FilterEvaluation.NoMatch;
        }

        if (!MatchesRequestedValues(
                filters.Locations,
                [document.Location, .. JobMetadataParser.Split(document.Tags)],
                "location",
                matchedFilters))
        {
            return FilterEvaluation.NoMatch;
        }

        if (!MatchesRequestedValues(
                filters.WorkModels,
                [document.WorkModel, .. JobMetadataParser.Split(document.Tags)],
                "work-model",
                matchedFilters))
        {
            return FilterEvaluation.NoMatch;
        }

        if (!MatchesSeniority(filters.Seniority, document, matchedFilters))
        {
            return FilterEvaluation.NoMatch;
        }

        return FilterEvaluation.Match(matchedFilters);
    }

    private static bool MatchesRequestedValues(
        IReadOnlyList<string> requestedValues,
        IReadOnlyList<string> sourceValues,
        string label,
        List<string> matchedFilters)
    {
        if (requestedValues.Count == 0)
        {
            return true;
        }

        var sourceTokens = BuildTokenSet(sourceValues);
        var matches = requestedValues
            .Where(value => ContainsAllTokens(sourceTokens, value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
        {
            return false;
        }

        matchedFilters.AddRange(matches.Select(value => $"{label}:{value}"));
        return true;
    }

    private static bool MatchesSeniority(
        IReadOnlyList<string> requestedValues,
        JobChunkDocument document,
        List<string> matchedFilters)
    {
        if (requestedValues.Count == 0)
        {
            return true;
        }

        var tokens = BuildTokenSet([document.Title, document.Tags, document.ChunkText]);
        var matches = requestedValues
            .Where(value => MatchesSeniority(value, tokens))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
        {
            return false;
        }

        matchedFilters.AddRange(matches.Select(value => $"seniority:{value}"));
        return true;
    }

    private static bool MatchesSeniority(string requestedValue, IReadOnlySet<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return false;
        }

        var normalized = QueryTextNormalizer.Normalize(requestedValue).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (!SeniorityAliases.TryGetValue(normalized, out var aliases))
        {
            aliases = [normalized];
        }

        if (normalized is "junior" && tokens.Contains("notjunior"))
        {
            return false;
        }

        return aliases.Any(tokens.Contains);
    }

    private static IReadOnlySet<string> BuildTokenSet(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(static value => QueryTextNormalizer.Tokenize(value!))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool ContainsAllTokens(IReadOnlySet<string> tokens, string value)
    {
        var valueTokens = QueryTextNormalizer.Tokenize(value);
        return valueTokens.Count > 0 && valueTokens.All(tokens.Contains);
    }

    private static bool IsEmpty(RetrievalFilters filters)
    {
        return filters.Technologies.Count == 0 &&
               filters.Seniority.Count == 0 &&
               filters.Categories.Count == 0 &&
               filters.Locations.Count == 0 &&
               filters.WorkModels.Count == 0;
    }
}

public sealed record FilterEvaluation(bool IsMatch, IReadOnlyList<string> MatchedFilters)
{
    public static FilterEvaluation NoMatch { get; } = new(false, []);

    public static FilterEvaluation Match(IReadOnlyList<string> matchedFilters) => new(true, matchedFilters);
}
