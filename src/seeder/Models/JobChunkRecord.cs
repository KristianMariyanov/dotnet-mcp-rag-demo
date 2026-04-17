namespace DotNetConf.Seeder.Models;

public sealed record JobChunkRecord(
    string ChunkId,
    string SourceJobId,
    int ChunkIndex,
    string Section,
    string ChunkText,
    string SearchText);
