using DotNetConf.Knowledge.Data;

namespace DotNetConf.Seeder.Models;

public sealed record JobVectorIndexResult(
    IReadOnlyList<JobChunkRecord> Chunks,
    IReadOnlyList<JobChunkDocument> Documents);
