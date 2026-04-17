namespace DotNetConf.RagServer.Contracts;

public sealed record RetrievalResponse(
    string QueryUsed,
    RetrievalFilters AppliedFilters,
    IReadOnlyList<RetrievalMatch> Matches);
