using DotNetConf.Seeder.Embedding;
using DotNetConf.Seeder.Cli;
using DotNetConf.Seeder.Services;
using DotNetConf.Seeder.Services.Chunking;
using Microsoft.Extensions.Configuration;

var parseResult = SeederCliParser.Parse(args);
if (!parseResult.Success)
{
    foreach (var error in parseResult.Errors)
    {
        Console.Error.WriteLine(error);
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine(SeederCliParser.GetUsage());
    return 1;
}

var options = parseResult.Options;
if (options.ShowHelp)
{
    Console.WriteLine(SeederCliParser.GetUsage());
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(options.DatabasePath) ?? ".");

using var httpClient = HttpClientFactory.Create();
var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();

var embeddingSettings = EmbeddingSettings.Load(configuration);
var embeddingGenerator = embeddingSettings.CreateGenerator();
var sitemapClient = new DevBgSitemapClient(httpClient);
var parser = new DevBgJobPageParser();
var chunkBuilder = new JobChunkBuilder();
var vectorIndexer = new JobVectorIndexer(chunkBuilder, embeddingGenerator, embeddingSettings.Mode);
var store = new SqliteJobStore(options.DatabasePath);
var seeder = new DevBgSeeder(httpClient, sitemapClient, parser, vectorIndexer, store);
var validator = new SeederValidationService(sitemapClient, store);

await store.InitializeAsync(CancellationToken.None);

if (!options.ValidateOnly)
{
    var summary = await seeder.SeedAsync(options, CancellationToken.None);
    Console.WriteLine(
        $"Seeded {summary.UpsertedJobs} job(s) from {summary.DiscoveredJobs} sitemap URL(s); " +
        $"{summary.SkippedJobs} unchanged job(s) skipped; {summary.UpsertedChunks} vector chunk(s) indexed with {summary.EmbeddingMode}; " +
        $"{summary.FailedJobs} failed, {summary.RemovedJobs} stale row(s) removed.");

    if (summary.FailedUrls.Count > 0)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("Failed job URLs:");
        foreach (var failedUrl in summary.FailedUrls)
        {
            Console.Error.WriteLine($" - {failedUrl}");
        }
    }

    if (summary.FailedJobs > 0)
    {
        return 1;
    }
}

if (options.SkipValidation)
{
    Console.WriteLine($"SQLite database: {options.DatabasePath}");
    return 0;
}

var validation = await validator.ValidateAsync(options, CancellationToken.None);
Console.WriteLine();
Console.WriteLine(
    $"Validation: sitemap has {validation.SitemapJobCount} job(s), database has {validation.DatabaseJobCount} job row(s), " +
    $"and {validation.VectorChunkCount} vector chunk row(s).");

if (validation.MissingUrls.Count == 0 &&
    validation.UnexpectedUrls.Count == 0 &&
    validation.JobsMissingVectors.Count == 0 &&
    validation.UnexpectedVectorJobs.Count == 0)
{
    Console.WriteLine("Validation passed. Every sitemap job URL is present and every stored job has vector chunks.");
    Console.WriteLine($"SQLite database: {options.DatabasePath}");
    return 0;
}

if (validation.MissingUrls.Count > 0)
{
    Console.Error.WriteLine("Missing database rows for sitemap URLs:");
    foreach (var url in validation.MissingUrls)
    {
        Console.Error.WriteLine($" - {url}");
    }
}

if (validation.UnexpectedUrls.Count > 0)
{
    Console.Error.WriteLine("Unexpected database rows not found in the sitemap:");
    foreach (var url in validation.UnexpectedUrls)
    {
        Console.Error.WriteLine($" - {url}");
    }
}

if (validation.JobsMissingVectors.Count > 0)
{
    Console.Error.WriteLine("Database jobs missing vector rows:");
    foreach (var jobId in validation.JobsMissingVectors)
    {
        Console.Error.WriteLine($" - {jobId}");
    }
}

if (validation.UnexpectedVectorJobs.Count > 0)
{
    Console.Error.WriteLine("Vector rows found for jobs that are not in the jobs table:");
    foreach (var jobId in validation.UnexpectedVectorJobs)
    {
        Console.Error.WriteLine($" - {jobId}");
    }
}

return 1;
