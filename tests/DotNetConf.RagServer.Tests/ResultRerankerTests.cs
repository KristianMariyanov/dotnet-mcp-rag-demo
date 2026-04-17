using DotNetConf.Knowledge.Data;
using DotNetConf.RagServer.Services;

namespace DotNetConf.RagServer.Tests;

public sealed class ResultRerankerTests
{
    private readonly ResultReranker _reranker = new();

    [Fact]
    public void Rerank_PrefersCandidateWithBetterLexicalAlignment()
    {
        var best = new AggregatedCandidate
        {
            Record = CreateDocument(
                chunkId: "chunk-1",
                title: "Senior .NET Backend Engineer",
                technologies: ".NET|Azure",
                text: "The team builds backend services in .NET and Azure."),
            BestSemanticScore = 0.74f
        };
        best.MatchedVariants.Add("senior dotnet backend role azure");
        best.SupportingChunks.Add(new ScoredChunk(best.Record, 0.74f));

        var weaker = new AggregatedCandidate
        {
            Record = CreateDocument(
                chunkId: "chunk-2",
                title: "BI Developer",
                technologies: "Power BI|SQL",
                text: "This role is focused on dashboards and reporting."),
            BestSemanticScore = 0.77f
        };
        weaker.MatchedVariants.Add("senior dotnet backend role azure");
        weaker.SupportingChunks.Add(new ScoredChunk(weaker.Record, 0.77f));

        var ranked = _reranker.Rerank(
            "senior dotnet backend role with azure",
            [weaker, best],
            2);

        Assert.Equal("chunk-1", ranked[0].Candidate.Record.ChunkId);
    }

    private static JobChunkDocument CreateDocument(string chunkId, string title, string technologies, string text)
    {
        return new JobChunkDocument
        {
            ChunkId = chunkId,
            JobId = $"job-{chunkId}",
            Url = $"https://dev.bg/job/{chunkId}",
            Title = title,
            Company = "Acme",
            Location = "Sofia",
            WorkModel = "Hybrid",
            EmploymentType = "FULL_TIME",
            Categories = "Backend Development",
            Technologies = technologies,
            Tags = $"{technologies}|Hybrid|notJunior",
            Section = "summary",
            ChunkText = text,
            SearchText = $"{title} Acme {technologies} {text}",
            PostedOn = "2026-04-16",
            IndexedAtUtc = "2026-04-16T08:00:00.0000000+00:00",
            Embedding = ReadOnlyMemory<float>.Empty
        };
    }
}
