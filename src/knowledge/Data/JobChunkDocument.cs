using Microsoft.Extensions.VectorData;

namespace DotNetConf.Knowledge.Data;

public sealed class JobChunkDocument
{
    [VectorStoreKey]
    public required string ChunkId { get; init; }

    [VectorStoreData]
    public required string JobId { get; init; }

    [VectorStoreData]
    public required string Url { get; init; }

    [VectorStoreData]
    public required string Title { get; init; }

    [VectorStoreData]
    public string Company { get; init; } = string.Empty;

    [VectorStoreData]
    public string Location { get; init; } = string.Empty;

    [VectorStoreData]
    public string WorkModel { get; init; } = string.Empty;

    [VectorStoreData]
    public string EmploymentType { get; init; } = string.Empty;

    [VectorStoreData]
    public string Categories { get; init; } = string.Empty;

    [VectorStoreData]
    public string Technologies { get; init; } = string.Empty;

    [VectorStoreData]
    public string Tags { get; init; } = string.Empty;

    [VectorStoreData]
    public string Section { get; init; } = string.Empty;

    [VectorStoreData]
    public string ChunkText { get; init; } = string.Empty;

    [VectorStoreData]
    public string SearchText { get; init; } = string.Empty;

    [VectorStoreData]
    public string PostedOn { get; init; } = string.Empty;

    [VectorStoreData]
    public string IndexedAtUtc { get; init; } = string.Empty;

    [VectorStoreVector(KnowledgeConstants.DefaultEmbeddingDimensions, DistanceFunction = DistanceFunction.CosineDistance)]
    public ReadOnlyMemory<float> Embedding { get; init; }
}
