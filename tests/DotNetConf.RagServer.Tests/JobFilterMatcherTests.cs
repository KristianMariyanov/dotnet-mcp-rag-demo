using DotNetConf.Knowledge.Data;
using DotNetConf.RagServer.Contracts;
using DotNetConf.RagServer.Services;

namespace DotNetConf.RagServer.Tests;

public sealed class JobFilterMatcherTests
{
    private readonly JobFilterMatcher _matcher = new();

    [Fact]
    public void Evaluate_MatchesTechnologyAndCategoryFilters()
    {
        var filters = new RetrievalFilters
        {
            Technologies = [".NET", "Azure"],
            Categories = ["Backend Development"]
        }.Normalize();

        var result = _matcher.Evaluate(CreateDocument(), filters);

        Assert.True(result.IsMatch);
        Assert.Contains("technology:.NET", result.MatchedFilters);
        Assert.Contains("category:Backend Development", result.MatchedFilters);
    }

    [Fact]
    public void Evaluate_RejectsJuniorFilter_WhenRoleIsExplicitlyNotJunior()
    {
        var filters = new RetrievalFilters
        {
            Seniority = ["junior"]
        }.Normalize();

        var result = _matcher.Evaluate(CreateDocument(), filters);

        Assert.False(result.IsMatch);
    }

    private static JobChunkDocument CreateDocument()
    {
        return new JobChunkDocument
        {
            ChunkId = "chunk-1",
            JobId = "job-1",
            Url = "https://dev.bg/job/1",
            Title = "Senior .NET Backend Engineer",
            Company = "Acme",
            Location = "Sofia",
            WorkModel = "Hybrid",
            EmploymentType = "FULL_TIME",
            Categories = "Backend Development|Cloud",
            Technologies = ".NET|Azure|SQL",
            Tags = ".NET|Azure|Hybrid|notJunior|Backend Development",
            Section = "summary",
            ChunkText = "Build backend services with .NET and Azure.",
            SearchText = "Senior .NET Backend Engineer Acme .NET Azure SQL",
            PostedOn = "2026-04-16",
            IndexedAtUtc = "2026-04-16T08:00:00.0000000+00:00",
            Embedding = ReadOnlyMemory<float>.Empty
        };
    }
}
