using System.Text.RegularExpressions;

namespace DotNetConf.RagServer.Services;

public sealed class ResultReranker
{
    public IReadOnlyList<RankedCandidate> Rerank(
        string query,
        IReadOnlyList<AggregatedCandidate> candidates,
        int count)
    {
        var queryTokens = Tokenize(query);

        return candidates
            .Select(candidate =>
            {
                var lexicalScore = ComputeLexicalScore(queryTokens, candidate.Record.SearchText);
                var combinedScore = candidate.BestSemanticScore * 0.5f + lexicalScore * 0.5f;
                return new RankedCandidate(candidate, combinedScore);
            })
            .OrderByDescending(static r => r.Score)
            .Take(count)
            .ToArray();
    }

    private static float ComputeLexicalScore(string[] queryTokens, string text)
    {
        if (queryTokens.Length == 0)
        {
            return 0f;
        }

        var textTokens = Tokenize(text);
        var matched = queryTokens.Count(queryToken =>
            textTokens.Any(textToken =>
                textToken.Contains(queryToken, StringComparison.Ordinal) ||
                queryToken.Contains(textToken, StringComparison.Ordinal)));

        return (float)matched / queryTokens.Length;
    }

    private static string[] Tokenize(string text)
    {
        return Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}

public sealed record RankedCandidate(AggregatedCandidate Candidate, float Score);
