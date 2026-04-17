namespace DotNetConf.RagServer.Contracts;

public sealed record RetrievalFilters
{
    public IReadOnlyList<string> Technologies { get; init; } = [];

    public IReadOnlyList<string> Seniority { get; init; } = [];

    public IReadOnlyList<string> Categories { get; init; } = [];

    public IReadOnlyList<string> Locations { get; init; } = [];

    public IReadOnlyList<string> WorkModels { get; init; } = [];

    public static RetrievalFilters Empty { get; } = new();

    public RetrievalFilters Normalize()
    {
        return new RetrievalFilters
        {
            Technologies = NormalizeValues(Technologies),
            Seniority = NormalizeValues(Seniority),
            Categories = NormalizeValues(Categories),
            Locations = NormalizeValues(Locations),
            WorkModels = NormalizeValues(WorkModels)
        };
    }

    private static IReadOnlyList<string> NormalizeValues(IReadOnlyList<string>? values)
    {
        return values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }
}
