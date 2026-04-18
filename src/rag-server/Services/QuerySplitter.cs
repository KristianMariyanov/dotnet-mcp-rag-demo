namespace DotNetConf.RagServer.Services;

public sealed class QuerySplitter
{
    public IReadOnlyList<string> Split(string input, int maxCount)
    {
        return input
            .Split('?', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static q => q.TrimEnd('.').Trim())
            .Where(static q => !string.IsNullOrWhiteSpace(q))
            .Take(maxCount)
            .ToArray();
    }
}
