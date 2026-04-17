namespace DotNetConf.Seeder.Models;

public sealed record SeedSummary(
    int DiscoveredJobs,
    int UpsertedJobs,
    int SkippedJobs,
    int FailedJobs,
    int RemovedJobs,
    int UpsertedChunks,
    string EmbeddingMode,
    IReadOnlyList<string> FailedUrls);
