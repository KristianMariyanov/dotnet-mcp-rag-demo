using DotNetConf.Knowledge.Data;
using DotNetConf.Seeder.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace DotNetConf.Seeder.Services;

public sealed class SqliteJobStore(string databasePath)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath
    }.ToString();

    private readonly VectorStoreCollection<string, JobChunkDocument> _vectorCollection =
        new SqliteCollection<string, JobChunkDocument>(
            new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString(),
            KnowledgeConstants.DefaultVectorCollectionName);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS job_sync_state (
                source_job_id TEXT NOT NULL PRIMARY KEY,
                url TEXT NOT NULL UNIQUE,
                sitemap_last_modified TEXT NULL,
                indexed_at_utc TEXT NOT NULL
            );

            DROP TABLE IF EXISTS jobs;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await _vectorCollection.EnsureCollectionExistsAsync(cancellationToken);
    }

    public Task UpsertAsync(
        DevBgJobPosting posting,
        JobVectorIndexResult result,
        CancellationToken cancellationToken) =>
        UpsertAsync(posting, result.Documents, cancellationToken);

    public async Task UpsertAsync(
        DevBgJobPosting posting,
        IReadOnlyList<JobChunkDocument> documents,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var existingChunkIds = await GetVectorChunkIdsByJobAsync(connection, posting.SourceJobId, cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO job_sync_state (
                    source_job_id, url, sitemap_last_modified, indexed_at_utc)
                VALUES (
                    $sourceJobId, $url, $sitemapLastModified, $indexedAtUtc)
                ON CONFLICT(source_job_id) DO UPDATE SET
                    url = excluded.url,
                    sitemap_last_modified = excluded.sitemap_last_modified,
                    indexed_at_utc = excluded.indexed_at_utc;
                """;

            Bind(command, "$sourceJobId", posting.SourceJobId);
            Bind(command, "$url", posting.Url);
            Bind(command, "$sitemapLastModified", posting.SitemapLastModified?.ToString("O"));
            Bind(command, "$indexedAtUtc", DateTimeOffset.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await _vectorCollection.UpsertAsync(documents, cancellationToken);

        var staleChunkIds = existingChunkIds
            .Except(documents.Select(document => document.ChunkId), StringComparer.Ordinal)
            .ToArray();

        if (staleChunkIds.Length > 0)
        {
            await _vectorCollection.DeleteAsync(staleChunkIds, cancellationToken);
        }
    }

    public async Task<int> DeleteMissingJobsAsync(
        IReadOnlySet<string> currentUrls,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var existingJobs = new List<StoredJobIdentity>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT source_job_id, url FROM job_sync_state;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existingJobs.Add(new StoredJobIdentity(reader.GetString(0), reader.GetString(1)));
            }
        }

        var staleJobs = existingJobs
            .Where(job => !currentUrls.Contains(job.Url))
            .ToArray();

        if (staleJobs.Length == 0)
        {
            return 0;
        }

        var staleJobIds = staleJobs
            .Select(job => job.SourceJobId)
            .ToArray();
        var staleChunkIds = await GetVectorChunkIdsByJobsAsync(connection, staleJobIds, cancellationToken);

        foreach (var staleJob in staleJobs)
        {
            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM job_sync_state WHERE url = $url;";
            deleteCommand.Parameters.AddWithValue("$url", staleJob.Url);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (staleChunkIds.Length > 0)
        {
            await _vectorCollection.DeleteAsync(staleChunkIds, cancellationToken);
        }

        return staleJobs.Length;
    }

    public async Task<IReadOnlyDictionary<string, DateTimeOffset?>> GetStoredSitemapLastModifiedByUrlsAsync(
        IReadOnlyList<string> urls,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase);
        if (urls.Count == 0)
        {
            return results;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        var parameterNames = new List<string>(urls.Count);

        for (var index = 0; index < urls.Count; index++)
        {
            var parameterName = $"$url{index}";
            parameterNames.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, urls[index]);
        }

        command.CommandText =
            $"""
            SELECT url, sitemap_last_modified
            FROM job_sync_state
            WHERE url IN ({string.Join(", ", parameterNames)});
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var url = reader.GetString(0);
            DateTimeOffset? sitemapLastModified = null;

            if (!reader.IsDBNull(1) && DateTimeOffset.TryParse(reader.GetString(1), out var parsedLastModified))
            {
                sitemapLastModified = parsedLastModified;
            }

            results[url] = sitemapLastModified;
        }

        return results;
    }

    public async Task<IReadOnlyList<string>> GetAllUrlsAsync(CancellationToken cancellationToken)
    {
        var urls = new List<string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT url FROM job_sync_state ORDER BY url;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            urls.Add(reader.GetString(0));
        }

        return urls;
    }

    public async Task<IReadOnlyList<string>> GetAllJobIdsAsync(CancellationToken cancellationToken)
    {
        var jobIds = new List<string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT source_job_id FROM job_sync_state ORDER BY source_job_id;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            jobIds.Add(reader.GetString(0));
        }

        return jobIds;
    }

    public async Task<int> GetVectorChunkCountAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {KnowledgeConstants.DefaultVectorCollectionName};";

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<string>> GetVectorJobIdsAsync(CancellationToken cancellationToken)
    {
        var results = new List<string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT DISTINCT JobId
            FROM {KnowledgeConstants.DefaultVectorCollectionName}
            ORDER BY JobId;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    private static async Task<string[]> GetVectorChunkIdsByJobAsync(
        SqliteConnection connection,
        string sourceJobId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT ChunkId
            FROM {KnowledgeConstants.DefaultVectorCollectionName}
            WHERE JobId = $sourceJobId
            ORDER BY ChunkId;
            """;
        command.Parameters.AddWithValue("$sourceJobId", sourceJobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var chunkIds = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            chunkIds.Add(reader.GetString(0));
        }

        return chunkIds.ToArray();
    }

    private static async Task<string[]> GetVectorChunkIdsByJobsAsync(
        SqliteConnection connection,
        IReadOnlyList<string> sourceJobIds,
        CancellationToken cancellationToken)
    {
        if (sourceJobIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        var parameterNames = new List<string>(sourceJobIds.Count);

        for (var index = 0; index < sourceJobIds.Count; index++)
        {
            var parameterName = $"$jobId{index}";
            parameterNames.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, sourceJobIds[index]);
        }

        command.CommandText =
            $"""
            SELECT ChunkId
            FROM {KnowledgeConstants.DefaultVectorCollectionName}
            WHERE JobId IN ({string.Join(", ", parameterNames)})
            ORDER BY ChunkId;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var chunkIds = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            chunkIds.Add(reader.GetString(0));
        }

        return chunkIds.ToArray();
    }

    private static void Bind(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private sealed record StoredJobIdentity(string SourceJobId, string Url);
}
