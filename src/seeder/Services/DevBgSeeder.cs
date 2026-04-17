using System.Collections.Concurrent;
using DotNetConf.Seeder.Cli;
using DotNetConf.Seeder.Models;

namespace DotNetConf.Seeder.Services;

public sealed class DevBgSeeder(
    HttpClient httpClient,
    DevBgSitemapClient sitemapClient,
    DevBgJobPageParser parser,
    JobVectorIndexer vectorIndexer,
    SqliteJobStore store)
{
    public async Task<SeedSummary> SeedAsync(SeederCliOptions options, CancellationToken cancellationToken)
    {
        var jobReferences = await sitemapClient.DiscoverJobReferencesAsync(options.SitemapIndexUrl, cancellationToken);
        if (options.Limit is { } limit)
        {
            jobReferences = jobReferences.Take(limit).ToArray();
        }

        Console.WriteLine($"Discovered {jobReferences.Count} job URL(s) from {options.SitemapIndexUrl}.");

        var failures = new ConcurrentBag<string>();
        var postings = new ConcurrentBag<DevBgJobPosting>();
        var upsertedJobs = 0;
        var skippedJobs = 0;
        var upsertedChunks = 0;
        var storedLastModifiedByUrl = await store.GetStoredSitemapLastModifiedByUrlsAsync(
            jobReferences.Select(reference => reference.Url).ToArray(),
            cancellationToken);
        var referencesToFetch = jobReferences
            .Where(reference => !ShouldSkip(reference, storedLastModifiedByUrl))
            .ToArray();

        skippedJobs = jobReferences.Count - referencesToFetch.Length;

        await Parallel.ForEachAsync(
            referencesToFetch,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = options.Concurrency,
                CancellationToken = cancellationToken
            },
            async (jobReference, token) =>
            {
                try
                {
                    var html = await GetJobPageAsync(jobReference.Url, token);
                    var posting = await parser.ParseAsync(jobReference.Url, html, jobReference.LastModified, token);
                    postings.Add(posting);
                }
                catch (Exception exception)
                {
                    failures.Add(jobReference.Url);
                    Console.Error.WriteLine($"Failed to fetch or parse {jobReference.Url}: {exception.Message}");
                }
            });

        var orderedPostings = postings
            .OrderBy(posting => posting.Url, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var index = 0; index < orderedPostings.Length; index++)
        {
            var posting = orderedPostings[index];
            Console.WriteLine($"[{index + 1}/{orderedPostings.Length}] Indexing {posting.SourceJobId} | {posting.Title}");

            try
            {
                var documents = await vectorIndexer.BuildAsync(posting, cancellationToken);
                await store.UpsertAsync(posting, documents, cancellationToken);
                upsertedJobs++;
                upsertedChunks += documents.Count;
                Console.WriteLine(
                    $"[{index + 1}/{orderedPostings.Length}] Stored {documents.Count} vector chunk(s) for {posting.Url}");
            }
            catch (Exception exception)
            {
                failures.Add(posting.Url);
                Console.Error.WriteLine($"[{index + 1}/{orderedPostings.Length}] Failed {posting.Url}: {exception.Message}");
            }
        }

        var removedJobs = await store.DeleteMissingJobsAsync(
            jobReferences
                .Select(reference => reference.Url)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            cancellationToken);

        return new SeedSummary(
            jobReferences.Count,
            upsertedJobs,
            skippedJobs,
            failures.Count,
            removedJobs,
            upsertedChunks,
            vectorIndexer.EmbeddingMode,
            failures.OrderBy(url => url, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool ShouldSkip(
        SitemapJobReference reference,
        IReadOnlyDictionary<string, DateTimeOffset?> storedLastModifiedByUrl)
    {
        if (!reference.LastModified.HasValue)
        {
            return false;
        }

        if (!storedLastModifiedByUrl.TryGetValue(reference.Url, out var storedLastModified) ||
            !storedLastModified.HasValue)
        {
            return false;
        }

        return storedLastModified.Value.ToUniversalTime() == reference.LastModified.Value.ToUniversalTime();
    }

    private async Task<string> GetJobPageAsync(string url, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception exception) when (attempt < 3)
            {
                lastException = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException($"Unable to fetch job page {url}.");
    }
}
