using DotNetConf.Knowledge.Data;
using DotNetConf.Seeder.Models;
using DotNetConf.Seeder.Services.Chunking;
using Microsoft.Extensions.AI;
using OpenAI.Embeddings;

namespace DotNetConf.Seeder.Services;

public sealed class JobVectorIndexer(
    JobChunkBuilder chunkBuilder,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    string embeddingMode)
{
    public string EmbeddingMode { get; } = embeddingMode;

    public async Task<IReadOnlyList<JobChunkDocument>> BuildAsync(
        DevBgJobPosting posting,
        CancellationToken cancellationToken)
    {
        var chunkRecords = await chunkBuilder.BuildAsync(posting, cancellationToken);
        if (chunkRecords.Count == 0)
        {
            throw new InvalidOperationException($"No chunks were produced for job {posting.Url}.");
        }

        var indexedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        var documents = new List<JobChunkDocument>(chunkRecords.Count);

        foreach (var chunkRecord in chunkRecords)
        {
            var embedding = await embeddingGenerator.GenerateAsync(chunkRecord.SearchText, cancellationToken: cancellationToken);

            documents.Add(new JobChunkDocument
            {
                ChunkId = chunkRecord.ChunkId,
                JobId = posting.SourceJobId,
                Url = posting.Url,
                Title = posting.Title,
                Company = posting.CompanyName ?? string.Empty,
                Location = posting.Location ?? string.Empty,
                WorkModel = posting.WorkModel ?? string.Empty,
                EmploymentType = posting.EmploymentType ?? string.Empty,
                Categories = string.Join("|", posting.Categories.Select(category => category.Name)),
                Technologies = string.Join("|", posting.Technologies.Select(technology => technology.Name)),
                Tags = string.Join("|", BuildTags(posting)),
                Section = chunkRecord.Section,
                ChunkText = chunkRecord.ChunkText,
                SearchText = chunkRecord.SearchText,
                PostedOn = posting.PostedOn?.ToString("yyyy-MM-dd") ?? string.Empty,
                IndexedAtUtc = indexedAtUtc,
                Embedding = embedding.Vector
            });
        }

        return documents;
    }

    private static IReadOnlyList<string> BuildTags(DevBgJobPosting posting)
    {
        var tags = new string?[]
        {
            posting.WorkModel,
            posting.EmploymentType,
            posting.JobType,
            posting.JobSubType,
            posting.Location,
            posting.SenioritySignal
        }
        .Concat(posting.Categories.Select(category => category.Name))
        .Concat(posting.Technologies.Select(technology => technology.Name))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Cast<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        return tags;
    }
}
