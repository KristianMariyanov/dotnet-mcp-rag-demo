using DotNetConf.Knowledge.Data;
using DotNetConf.RagServer.Contracts;
using DotNetConf.RagServer.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;

namespace DotNetConf.RagServer.Services;

public sealed class RetrievalHealthService(
    VectorStoreCollection<string, JobChunkDocument> collection,
    IOptions<EmbeddingOptions> embeddingOptions,
    IOptions<RetrievalOptions> retrievalOptions)
{
    public async Task EnsureStoreReadyAsync(CancellationToken cancellationToken)
    {
        await collection.EnsureCollectionExistsAsync(cancellationToken);
    }

    public async Task<RetrievalHealthResponse> CheckAsync(CancellationToken cancellationToken)
    {
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        return new RetrievalHealthResponse(
            "ok",
            retrievalOptions.Value.CollectionName,
            embeddingOptions.Value.Model);
    }
}
