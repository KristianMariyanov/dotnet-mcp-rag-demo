# dotnet-mcp-rag-demo

> A .NET RAG demo built for .NETworking  Conf. An AI assistant can search real job postings from [dev.bg](https://dev.bg) through a local MCP tool, backed by semantic vector search over an offline-indexed SQLite database.

## Architecture Overview

The system is made up of three independently runnable components plus a shared library:

```
AI Assistant (Claude Desktop, VS Code Copilot, MCP Inspector, …)
  │
  │  MCP over stdio
  ▼
┌──────────────────────────────────┐
│  mcp  (MCP server)               │  exposes search_jobs tool
│  stdio process, no LLM calls     │
└──────────────┬───────────────────┘
               │  HTTP POST /retrieve  (loopback)
               ▼
┌──────────────────────────────────┐
│  rag-server  (Retrieval API)     │  ASP.NET Core minimal API
│  embeds query → vector search    │  on http://localhost:5100
│  → metadata filtering → results  │
└──────────────┬───────────────────┘
               │  reads
               ▼
┌──────────────────────────────────┐
│  artifacts/devbg-jobs-full.db    │  SQLite + sqlite-vec
│  job rows + vector chunks        │
└──────────────────────────────────┘

(offline, run once)
┌──────────────────────────────────┐
│  seeder  (Indexing CLI)          │  crawls dev.bg sitemap
│  parse → chunk → embed → upsert  │  writes to the same SQLite DB
└──────────────────────────────────┘
```

### Data flow — query path

1. The user asks a question inside an MCP-aware client.
2. The model calls `search_jobs` on the local MCP server with a query string and optional structured filters.
3. The MCP server forwards the request over HTTP to `rag-server`.
4. `rag-server` embeds the query with OpenAI `text-embedding-3-small`, runs cosine similarity search against the SQLite vector store, applies post-filter logic (technology, seniority, category, location, work model), deduplicates by job, and returns the top matches.
5. The MCP server serialises the result and hands it back to the model as grounded context.

### Data flow — indexing path (offline)

1. The `seeder` CLI fetches the dev.bg sitemap index and discovers all job URLs.
2. Each job page is parsed: title, company, location, work model, employment type, categories, technologies, tags, description HTML.
3. The description is split into semantically coherent text chunks (~900 characters) plus a compact overview chunk.
4. Each chunk is enriched with job metadata and embedded via OpenAI or Azure OpenAI.
5. All rows are upserted into a SQLite database that also holds the `sqlite-vec` virtual table for nearest-neighbour search.

## Projects

| Folder | Project | Role |
|--------|---------|------|
| `src/knowledge` | `DotNetConf.Knowledge` | Shared library — `JobChunkDocument` vector schema and constants used by both seeder and rag-server |
| `src/seeder` | `DotNetConf.Seeder` | Offline CLI that crawls dev.bg, chunks and embeds job postings, and writes them to SQLite |
| `src/rag-server` | `DotNetConf.RagServer` | ASP.NET Core retrieval API — embeds queries, searches the vector store, filters results |
| `src/mcp` | `DotNetConf.Mcp` | Stdio MCP server — exposes the `search_jobs` tool and proxies calls to rag-server |
| `src/client` | — | Placeholder for a future reference UI (not implemented) |

## Running the Demo

### Prerequisites

- .NET 10 SDK
- An OpenAI API key (or Azure OpenAI credentials for the seeder)
- A populated SQLite database in `artifacts/` (produced by the seeder)

### 1 — Run the retrieval server

```bash
cd src/rag-server
dotnet user-secrets set "Embedding:ApiKey" "sk-..."
dotnet run
```

The server starts on `http://localhost:5100`. Check `/health` to confirm the vector collection is reachable.

### 2 — Configure the MCP server

Add the following to your MCP client configuration (e.g. `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "dotnet-jobs": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/mcp"]
    }
  }
}
```

The MCP server connects to `http://127.0.0.1:5100/` by default. Override with `RetrievalBackend__BaseUrl`.

### 3 — (Re)seed the database

```bash
cd src/seeder
dotnet user-secrets set "Embedding:ApiKey" "sk-..."
dotnet run -- --database ../../artifacts/devbg-jobs.db
```

Run `dotnet run -- --help` for all CLI options (`--limit`, `--concurrency`, `--validate-only`, etc.).

## Configuration

### rag-server — `appsettings.json`

| Key | Default | Description |
|-----|---------|-------------|
| `Embedding:Model` | `text-embedding-3-small` | OpenAI embedding model |
| `Embedding:ApiKey` | *(required)* | OpenAI API key |
| `Embedding:Dimensions` | `1536` | Must match the stored vectors |
| `Retrieval:CandidateCount` | `20` | Vector search over-fetch before filtering |
| `Retrieval:ResultCount` | `5` | Final matches returned |
| `Retrieval:MinimumSemanticScore` | `0.35` | Cosine similarity threshold |
| `ConnectionStrings:VectorDb` | relative path to `.db` | SQLite database file |

### mcp — `appsettings.json`

| Key | Default | Description |
|-----|---------|-------------|
| `RetrievalBackend:BaseUrl` | `http://127.0.0.1:5100/` | URL of the running rag-server |
| `RetrievalBackend:TimeoutSeconds` | `20` | HTTP timeout |

## Repository Layout

```
src/
  knowledge/    shared vector schema library
  seeder/       offline indexing CLI
  rag-server/   retrieval HTTP API
  mcp/          stdio MCP server
  client/       (future)
artifacts/      SQLite database files (git-ignored)
tests/
```


