using System.Net.Http.Json;
using System.Text.Json;

namespace DotNetConf.Mcp;

internal sealed class RetrievalBackendClient(HttpClient httpClient)
{
    public async Task<SearchJobsResponse> SearchJobsAsync(string query, int? resultCount, SearchJobsRequestFilters filters, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "retrieve",
            new SearchJobsRequest
            {
                Query = query,
                ResultCount = resultCount,
                Filters = filters
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new RetrievalBackendException(await ReadErrorMessageAsync(response, cancellationToken), response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<SearchJobsResponse>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            throw new RetrievalBackendException("The retrieval server returned an empty response.", response.StatusCode);
        }

        return payload;
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"The retrieval server returned {(int)response.StatusCode} {response.ReasonPhrase}.";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errorsElement) &&
                errorsElement.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();
                foreach (var property in errorsElement.EnumerateObject())
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            messages.Add(value);
                        }
                    }
                }

                if (messages.Count > 0)
                {
                    return string.Join(" ", messages);
                }
            }

            if (root.TryGetProperty("detail", out var detailElement) &&
                detailElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(detailElement.GetString()))
            {
                return detailElement.GetString()!;
            }

            if (root.TryGetProperty("title", out var titleElement) &&
                titleElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(titleElement.GetString()))
            {
                return titleElement.GetString()!;
            }
        }
        catch (JsonException)
        {
        }

        return $"The retrieval server returned {(int)response.StatusCode} {response.ReasonPhrase}.";
    }
}

internal sealed class RetrievalBackendException(string message, System.Net.HttpStatusCode? statusCode = null) : Exception(message)
{
    public System.Net.HttpStatusCode? StatusCode { get; } = statusCode;
}

internal sealed record SearchJobsRequest
{
    public string Query { get; init; } = string.Empty;

    public int? ResultCount { get; init; }

    public SearchJobsRequestFilters? Filters { get; init; }
}

internal sealed record SearchJobsRequestFilters
{
    public IReadOnlyList<string> Technologies { get; init; } = [];

    public IReadOnlyList<string> Seniority { get; init; } = [];

    public IReadOnlyList<string> Categories { get; init; } = [];

    public IReadOnlyList<string> Locations { get; init; } = [];

    public IReadOnlyList<string> WorkModels { get; init; } = [];
}

internal sealed record SearchJobsResponse(
    string QueryUsed,
    SearchJobsFilters AppliedFilters,
    IReadOnlyList<SearchJobsMatch> Matches);

internal sealed record SearchJobsFilters(
    IReadOnlyList<string> Technologies,
    IReadOnlyList<string> Seniority,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Locations,
    IReadOnlyList<string> WorkModels);

internal sealed record SearchJobsMatch(
    string JobId,
    string Title,
    string Company,
    string Location,
    string WorkModel,
    string EmploymentType,
    string Seniority,
    string Url,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<string> Tags,
    string Highlight,
    float Score);