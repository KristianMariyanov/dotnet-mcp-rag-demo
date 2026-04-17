using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DotNetConf.Mcp;

[McpServerToolType]
internal sealed class SearchJobsTool(RetrievalBackendClient backendClient, ILogger<SearchJobsTool> logger)
{
    [McpServerTool(Name = "search_jobs")]
    [Description("""
        Searches job postings via the local .NET RAG retrieval server using semantic vector search
        combined with structured metadata filters. Returns a ranked list of matching job chunks with
        title, company, location, seniority, matched filters, and a relevance score.

        Filtering rules:
        - ALL provided filter arrays must match (AND logic across filter types).
        - Within a single filter array, AT LEAST ONE value must match (OR logic within the array).
        - Matching is token-based and case-insensitive. A multi-word value like "C# .NET" requires
          every token to appear in the document field.
        - Omitting a filter array (or passing null/empty) skips that filter entirely.
        """)]
    public async Task<CallToolResult> SearchJobsAsync(
        [Description("""
            The semantic search text describing the desired role or skills.
            Write natural language, e.g. "backend engineer with cloud experience" rather than just listing keywords.
            Max 800 characters.
            """)] string query,
        [Description("""
            Number of results to return. Must be between 1 and 10. Defaults to the server default (5) when omitted.
            Use a smaller value (1–3) for focused queries; use a larger value (8–10) for broad exploration.
            """)] int? resultCount = null,
        [Description("""
            Technology filter. Pass one or more technology names; a job matches if at least one of them
            appears in its Technologies, Tags, or Title fields.
            Examples: ["C#"], [".NET", "Azure"], ["React", "TypeScript"], ["Python", "FastAPI"].
            Multi-word values are supported: ".NET Core" requires both tokens to match.
            """)] string[]? technologies = null,
        [Description("""
            Seniority filter. Pass one or more levels; a job matches if its Title, Tags, or description
            imply at least one of the requested levels via alias expansion:
              "junior"          → matches: junior, entry, intern, graduate, trainee
              "mid" / "regular" → matches: mid, middle, regular, intermediate, experienced, notjunior
              "senior"          → matches: senior, lead, principal, staff, architect, expert, notjunior
            "mid" and "regular" are equivalent. Any other value is matched literally.
            Example: ["senior"] or ["junior", "mid"].
            """)] string[]? seniority = null,
        [Description("""
            Job category filter. Pass one or more functional categories; a job matches if at least one
            appears in its Categories, Tags, or Title fields.
            Examples: ["Backend"], ["Frontend", "Mobile"], ["DevOps"], ["Data Science", "ML"].
            """)] string[]? categories = null,
        [Description("""
            Location filter. Pass one or more city names or "Remote"; a job matches if at least one
            appears in its Location field or Tags.
            Examples: ["Sofia"], ["Plovdiv", "Varna"], ["Remote"], ["Sofia", "Remote"].
            Partial tokens work — "Sofia" matches "Sofia, Bulgaria".
            """)] string[]? locations = null,
        [Description("""
            Work model filter. Pass one or more work arrangement types; a job matches if at least one
            appears in its WorkModel field or Tags.
            Known values: "Remote", "Hybrid", "On-site" (case-insensitive).
            Examples: ["Remote"], ["Hybrid", "Remote"], ["On-site"].
            """)] string[]? workModels = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Error("Query is required.");
        }

        if (query.Length > 800)
        {
            return Error("Query must be 800 characters or less.");
        }

        if (resultCount is < 1 or > 10)
        {
            return Error("Result count must be between 1 and 10.");
        }

        var filters = new SearchJobsRequestFilters
        {
            Technologies = technologies ?? [],
            Seniority = seniority ?? [],
            Categories = categories ?? [],
            Locations = locations ?? [],
            WorkModels = workModels ?? []
        };

        try
        {
            var response = await backendClient.SearchJobsAsync(query.Trim(), resultCount, filters, cancellationToken);
            var text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false });

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = text }],
                StructuredContent = JsonSerializer.SerializeToElement(response)
            };
        }
        catch (RetrievalBackendException ex)
        {
            logger.LogWarning(ex, "Job search failed with a backend response error.");
            return Error(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Job search failed because the retrieval server could not be reached.");
            return Error("The retrieval server is unavailable. Start rag-server and verify RetrievalBackend__BaseUrl.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Job search timed out while waiting for the retrieval server.");
            return Error("The retrieval server timed out.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Job search failed because the retrieval server response could not be parsed.");
            return Error("The retrieval server returned an invalid response.");
        }
    }

    private static CallToolResult Error(string message)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = message }]
        };
    }
}