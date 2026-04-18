using DotNetConf.Knowledge.Data;

namespace DotNetConf.RagServer.Services;

public sealed class AggregatedCandidate
{
    public required JobChunkDocument Record { get; init; }

    public required float BestSemanticScore { get; init; }

    public List<string> MatchedVariants { get; } = [];

    public List<ScoredChunk> SupportingChunks { get; } = [];
}

public sealed record ScoredChunk(JobChunkDocument Record, float Score);
