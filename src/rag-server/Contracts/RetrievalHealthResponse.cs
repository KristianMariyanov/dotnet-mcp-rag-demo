namespace DotNetConf.RagServer.Contracts;

public sealed record RetrievalHealthResponse(
    string Status,
    string CollectionName,
    string EmbeddingMode);
