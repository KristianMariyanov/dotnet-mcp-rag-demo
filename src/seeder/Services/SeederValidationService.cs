using DotNetConf.Seeder.Cli;
using DotNetConf.Seeder.Models;

namespace DotNetConf.Seeder.Services;

public sealed class SeederValidationService(
    DevBgSitemapClient sitemapClient,
    SqliteJobStore store)
{
    public async Task<ValidationSummary> ValidateAsync(SeederCliOptions options, CancellationToken cancellationToken)
    {
        var sitemapReferences = await sitemapClient.DiscoverJobReferencesAsync(options.SitemapIndexUrl, cancellationToken);
        if (options.Limit is { } limit)
        {
            sitemapReferences = sitemapReferences.Take(limit).ToArray();
        }

        var sitemapUrls = sitemapReferences
            .Select(reference => reference.Url)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var databaseUrls = (await store.GetAllUrlsAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var databaseJobIds = (await store.GetAllJobIdsAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vectorJobIds = (await store.GetVectorJobIdsAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingUrls = sitemapUrls
            .Except(databaseUrls, StringComparer.OrdinalIgnoreCase)
            .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var unexpectedUrls = databaseUrls
            .Except(sitemapUrls, StringComparer.OrdinalIgnoreCase)
            .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var jobsMissingVectors = databaseJobIds
            .Except(vectorJobIds, StringComparer.OrdinalIgnoreCase)
            .OrderBy(jobId => jobId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var unexpectedVectorJobs = vectorJobIds
            .Except(databaseJobIds, StringComparer.OrdinalIgnoreCase)
            .OrderBy(jobId => jobId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ValidationSummary(
            sitemapUrls.Count,
            databaseUrls.Count,
            await store.GetVectorChunkCountAsync(cancellationToken),
            missingUrls,
            unexpectedUrls,
            jobsMissingVectors,
            unexpectedVectorJobs);
    }
}
