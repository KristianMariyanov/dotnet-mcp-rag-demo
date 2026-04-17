using DotNetConf.Knowledge.Data;
using DotNetConf.Seeder.Models;
using DotNetConf.Seeder.Services;

namespace DotNetConf.Seeder.Tests;

public sealed class SqliteJobStoreTests
{
    [Fact]
    public async Task UpsertAsync_PersistsJobsChunksAndVectors_AndDeletesStaleRows()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

        var store = new SqliteJobStore(databasePath);
        await store.InitializeAsync(CancellationToken.None);

        await store.UpsertAsync(
            CreatePosting("job-1", "https://dev.bg/company/jobads/job-1/"),
            CreateVectorIndexResult("job-1", "https://dev.bg/company/jobads/job-1/"),
            CancellationToken.None);

        await store.UpsertAsync(
            CreatePosting("job-2", "https://dev.bg/company/jobads/job-2/"),
            CreateVectorIndexResult("job-2", "https://dev.bg/company/jobads/job-2/"),
            CancellationToken.None);

        var removedRows = await store.DeleteMissingJobsAsync(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "https://dev.bg/company/jobads/job-1/" },
            CancellationToken.None);

        var urls = await store.GetAllUrlsAsync(CancellationToken.None);
        var vectorChunkCount = await store.GetVectorChunkCountAsync(CancellationToken.None);
        var vectorJobIds = await store.GetVectorJobIdsAsync(CancellationToken.None);

        Assert.Equal(1, removedRows);
        Assert.Single(urls);
        Assert.Equal("https://dev.bg/company/jobads/job-1/", urls[0]);
        Assert.Equal(1, vectorChunkCount);
        Assert.Single(vectorJobIds);
        Assert.Equal("job-1", vectorJobIds[0]);
    }

    private static DevBgJobPosting CreatePosting(string sourceJobId, string url) =>
        new(
            SourceJobId: sourceJobId,
            Url: url,
            CanonicalUrl: url,
            Slug: sourceJobId,
            Title: $"Title {sourceJobId}",
            CompanyName: "Acme",
            CompanyUrl: "https://dev.bg/company/acme/",
            CompanyLogoUrl: "https://dev.bg/logo.png",
            CompanySummary: "Acme builds products.",
            CompanyJobsUrl: "https://dev.bg/company/acme/#jobs",
            CompanyOpenJobsCount: 3,
            Location: "Sofia",
            WorkModel: "Hybrid",
            WorkModelDetails: "Hybrid schedule",
            PostedOn: new DateOnly(2026, 4, 16),
            ValidThrough: null,
            EmploymentType: "FULL_TIME",
            JobType: "Backend Development",
            JobSubType: ".NET",
            PaymentType: "paid",
            OnlyInDevBg: "nonExclusive",
            SenioritySignal: "notJunior",
            QualityScore: 2,
            ValueScore: 200,
            SalaryText: null,
            OgDescription: "Example",
            DescriptionHtml: "<p>Example job description.</p>",
            DescriptionText: "Example job description.",
            SitemapLastModified: new DateTimeOffset(2026, 4, 16, 8, 0, 0, TimeSpan.Zero),
            Categories:
            [
                new JobCategory(".NET", "https://dev.bg/company/jobs/net/", 108)
            ],
            Technologies:
            [
                new JobTechnology("C#", 0)
            ],
            MetadataJson: """{"sourceSite":"dev.bg"}""");

    private static JobVectorIndexResult CreateVectorIndexResult(string sourceJobId, string url)
    {
        var chunkId = $"devbg:{sourceJobId}:000";
        return new JobVectorIndexResult(
            [
                new JobChunkRecord(
                    ChunkId: chunkId,
                    SourceJobId: sourceJobId,
                    ChunkIndex: 0,
                    Section: "overview",
                    ChunkText: "Example job description.",
                    SearchText: "Title Acme Example job description.")
            ],
            [
                new JobChunkDocument
                {
                    ChunkId = chunkId,
                    JobId = sourceJobId,
                    Url = url,
                    Title = $"Title {sourceJobId}",
                    Company = "Acme",
                    Location = "Sofia",
                    WorkModel = "Hybrid",
                    EmploymentType = "FULL_TIME",
                    Categories = ".NET",
                    Technologies = "C#",
                    Tags = ".NET|C#|Hybrid",
                    Section = "overview",
                    ChunkText = "Example job description.",
                    SearchText = "Title Acme Example job description.",
                    PostedOn = "2026-04-16",
                    IndexedAtUtc = "2026-04-16T08:00:00.0000000+00:00",
                    Embedding = new float[KnowledgeConstants.DefaultEmbeddingDimensions]
                }
            ]);
    }
}
