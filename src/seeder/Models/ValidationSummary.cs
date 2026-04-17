namespace DotNetConf.Seeder.Models;

public sealed record ValidationSummary(
    int SitemapJobCount,
    int DatabaseJobCount,
    int VectorChunkCount,
    IReadOnlyList<string> MissingUrls,
    IReadOnlyList<string> UnexpectedUrls,
    IReadOnlyList<string> JobsMissingVectors,
    IReadOnlyList<string> UnexpectedVectorJobs);
