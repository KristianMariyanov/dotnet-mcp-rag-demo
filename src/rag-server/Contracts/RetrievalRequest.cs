namespace DotNetConf.RagServer.Contracts;

public sealed record RetrievalRequest
{
    public string Query { get; init; } = string.Empty;

    public RetrievalFilters? Filters { get; init; }

    public int? ResultCount { get; init; }
}
