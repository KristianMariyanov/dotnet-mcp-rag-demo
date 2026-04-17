using DotNetConf.Knowledge.Data;
using DotNetConf.RagServer.Contracts;
using DotNetConf.RagServer.Services;

namespace DotNetConf.RagServer.Tests;

public sealed class JobFitAdvisorTests
{
    private readonly JobFitAdvisor _advisor = new();

    [Fact]
    public void Analyze_IdentifiesOverlapAndGaps()
    {
        var candidate = new AggregatedCandidate
        {
            Record = new JobChunkDocument
            {
                ChunkId = "chunk-1",
                JobId = "job-1",
                Url = "https://dev.bg/job/1",
                Title = "Backend Engineer",
                Company = "Acme",
                Location = "Sofia",
                WorkModel = "Hybrid",
                EmploymentType = "FULL_TIME",
                Categories = "Backend Development",
                Technologies = ".NET|Java|Azure|SQL",
                Tags = ".NET|Java|Azure|SQL|Hybrid",
                Section = "summary",
                ChunkText = "Build .NET and Java services with Azure and SQL.",
                SearchText = "Backend Engineer Acme .NET Java Azure SQL",
                PostedOn = "2026-04-16",
                IndexedAtUtc = "2026-04-16T08:00:00.0000000+00:00",
                Embedding = ReadOnlyMemory<float>.Empty
            },
            BestSemanticScore = 0.82f
        };

        var analysis = _advisor.Analyze(
            query: "find me a backend role",
            candidateProfile: "Junior .NET and Java developer",
            filters: new RetrievalFilters
            {
                Technologies = [".NET", "Java"],
                Seniority = ["junior"]
            }.Normalize(),
            candidate: candidate);

        Assert.Contains(".NET", analysis.MatchedSignals);
        Assert.Contains("Java", analysis.MatchedSignals);
        Assert.Contains("Azure", analysis.MissingSignals);
        Assert.Contains("SQL", analysis.MissingSignals);
    }
}
