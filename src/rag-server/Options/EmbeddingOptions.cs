using DotNetConf.Knowledge.Data;

namespace DotNetConf.RagServer.Options;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public string Model { get; init; } = "text-embedding-3-small";

    public string? ApiKey { get; init; }

    public int Dimensions { get; init; } = KnowledgeConstants.DefaultEmbeddingDimensions;
}
