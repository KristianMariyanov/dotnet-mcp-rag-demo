using DotNetConf.Knowledge.Data;
using DotNetConf.RagServer.Contracts;
using DotNetConf.RagServer.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;

namespace DotNetConf.RagServer.Services;

public sealed class RetrievalService(
    VectorStoreCollection<string, JobChunkDocument> collection,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    JobFilterMatcher filterMatcher,
    IOptions<RetrievalOptions> retrievalOptions,
    ILogger<RetrievalService> logger)
{
    public async Task<RetrievalResponse> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken)
    {
        var options = retrievalOptions.Value;
        var normalizedFilters = (request.Filters ?? RetrievalFilters.Empty).Normalize();
        var queryUsed = BuildQueryText(request.Query, normalizedFilters);

        var embeddings = await embeddingGenerator.GenerateAsync([queryUsed], cancellationToken: cancellationToken);
        var vectorMatches = collection.SearchAsync(embeddings[0].Vector, top: options.CandidateCount, cancellationToken: cancellationToken);

        // Deduplicate by JobId, keeping the chunk with the highest score
        var best = new Dictionary<string, (JobChunkDocument Record, float Score)>(StringComparer.Ordinal);

        await foreach (var match in vectorMatches.WithCancellation(cancellationToken))
        {
            if (match.Record is null)
            {
                continue;
            }

            var score = (float)(match.Score ?? 0d);

            if (score < options.MinimumSemanticScore)
            {
                continue;
            }

            if (!filterMatcher.Evaluate(match.Record, normalizedFilters).IsMatch)
            {
                continue;
            }

            if (!best.TryGetValue(match.Record.JobId, out var existing) || score > existing.Score)
            {
                best[match.Record.JobId] = (match.Record, score);
            }
        }

        var matches = best.Values
            .OrderByDescending(static c => c.Score)
            .Take(request.ResultCount ?? options.ResultCount)
            .Select(static c =>
            {
                var r = c.Record;
                return new RetrievalMatch(
                    r.JobId,
                    r.Title,
                    r.Company,
                    r.Location,
                    r.WorkModel,
                    r.EmploymentType,
                    JobMetadataParser.InferSeniority(r),
                    r.Url,
                    JobMetadataParser.Split(r.Categories),
                    JobMetadataParser.Split(r.Technologies),
                    JobMetadataParser.Split(r.Tags),
                    r.ChunkText,
                    c.Score);
            })
            .ToArray();

        logger.LogInformation("Retrieval returned {MatchCount} match(es) for query: {Query}", matches.Length, queryUsed);

        return new RetrievalResponse(queryUsed, normalizedFilters, matches);
    }

    private static string BuildQueryText(string query, RetrievalFilters filters)
    {
        var parts = new List<string> { query };

        parts.AddRange(filters.Technologies);
        parts.AddRange(filters.Seniority);
        parts.AddRange(filters.Categories);
        parts.AddRange(filters.Locations);
        parts.AddRange(filters.WorkModels);

        return QueryTextNormalizer.Normalize(
            string.Join(". ", parts.Where(static v => !string.IsNullOrWhiteSpace(v))));
    }
}
