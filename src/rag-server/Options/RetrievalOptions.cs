using DotNetConf.Knowledge.Data;

namespace DotNetConf.RagServer.Options;

public sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    public string CollectionName { get; init; } = KnowledgeConstants.DefaultVectorCollectionName;

    public int CandidateCount { get; init; } = 12;

    public int ResultCount { get; init; } = 5;

    public float MinimumSemanticScore { get; init; } = 0.35f;
}
