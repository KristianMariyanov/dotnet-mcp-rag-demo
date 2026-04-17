# mcp

Stdio MCP server that exposes a single tool — `search_jobs` — to any MCP-aware AI client. It is a thin protocol adapter: it validates the tool call, forwards it to `rag-server` over HTTP, and hands the structured result back to the model.

This project does not call an LLM, build prompts, manage embeddings, or query the vector store directly.

## Tool — `search_jobs`

Registered with the MCP SDK via `[McpServerTool]` and `[McpServerToolType]`.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | `string` | Yes | Free-text search query, max 800 characters |
| `resultCount` | `int?` | No | Number of results, 1–10 |
| `technologies` | `string[]?` | No | Filter by technology (e.g. `"C#"`, `".NET"`, `"React"`) |
| `seniority` | `string[]?` | No | Filter by seniority (e.g. `"Junior"`, `"Senior"`) |
| `categories` | `string[]?` | No | Filter by job category (e.g. `"Backend"`, `"DevOps"`) |
| `locations` | `string[]?` | No | Filter by location (e.g. `"Sofia"`, `"Remote"`) |
| `workModels` | `string[]?` | No | Filter by work arrangement (e.g. `"Remote"`, `"Hybrid"`) |

The tool returns a JSON-serialised `SearchJobsResponse` as both a `TextContentBlock` and `StructuredContent` on the `CallToolResult`.

## How it works

1. The MCP client sends a `tools/call` request over stdio.
2. `SearchJobsTool.SearchJobsAsync` validates the inputs and builds a `SearchJobsRequest`.
3. `RetrievalBackendClient` posts the request to `{BaseUrl}/retrieve` on the running rag-server.
4. The response is serialised to JSON and returned to the model.

Error handling covers: backend validation errors (HTTP 4xx with ProblemDetails), unreachable server (`HttpRequestException`), timeout (`TaskCanceledException`), and malformed response (`JsonException`).

## Transport

The server uses **stdio transport** (`WithStdioServerTransport()`). All diagnostic logs are written to `stderr` to keep `stdout` clean for the MCP protocol stream.

## Configuration

```json
// appsettings.json
{
  "RetrievalBackend": {
    "BaseUrl": "http://127.0.0.1:5100/",
    "TimeoutSeconds": 20
  }
}
```

`BaseUrl` can also be set via the environment variable `RetrievalBackend__BaseUrl`.

## Running as an MCP server

Add to your client's MCP config (e.g. `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "dotnet-jobs": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/src/mcp"],
      "env": {
        "RetrievalBackend__BaseUrl": "http://127.0.0.1:5100/"
      }
    }
  }
}
```

The rag-server must already be running before the MCP client invokes the tool.
