# rag-server

ASP.NET Core minimal API that handles job retrieval. It accepts an inbound query, embeds it, searches the SQLite vector store, applies post-filter logic, and returns the top-scored job matches.

The rag-server does not call an LLM and does not generate prose. It is purely a retrieval pipeline.

## Endpoints

### `GET /health`

Returns the current status of the vector collection, the collection name, and the embedding model in use. Called by the MCP server at startup to confirm the store is reachable.

### `POST /retrieve`

Main retrieval endpoint. Accepts a JSON body.

**Request**

```json
{
  "query": "senior .NET backend engineer in Sofia with Azure",
  "resultCount": 5,
  "filters": {
    "technologies": ["C#", ".NET"],
    "seniority": ["senior"],
    "categories": ["backend"],
    "locations": ["Sofia"],
    "workModels": ["hybrid"]
  }
}
```

All fields except `query` are optional. `resultCount` defaults to the server-side `Retrieval:ResultCount` setting.

**Response**

```json
{
  "queryUsed": "senior csharp dotnet backend engineer Sofia Azure",
  "filters": { ... },
  "matches": [
    {
      "jobId": "...",
      "title": "Senior .NET Engineer",
      "company": "Acme Ltd",
      "location": "Sofia",
      "workModel": "Hybrid",
      "employmentType": "Full-time",
      "seniority": "senior",
      "url": "https://dev.bg/...",
      "categories": ["Backend"],
      "technologies": ["C#", ".NET", "Azure"],
      "tags": ["senior", "hybrid"],
      "chunkText": "...",
      "score": 0.87
    }
  ]
}
```

## Retrieval Pipeline

1. **Query normalisation** — tech abbreviations are expanded (`C#` → `csharp`, `.NET` → `dotnet`) so they survive tokenisation reliably.
2. **Embedding** — the normalised query is passed to `IEmbeddingGenerator<string, Embedding<float>>` backed by OpenAI `text-embedding-3-small`.
3. **Vector search** — `SqliteCollection.SearchAsync` returns up to `CandidateCount` (default 20) nearest neighbours by cosine similarity.
4. **Score threshold** — chunks below `MinimumSemanticScore` (default 0.35) are discarded.
5. **Post-filter** — `JobFilterMatcher` applies the requested structured filters:
   - `technologies` — matched against the chunk's technology list, tags, and title
   - `categories` — matched against categories, tags, and title
   - `locations` — matched against the location field and tags
   - `workModels` — matched against the work model field and tags
   - `seniority` — inferred from job title and text via keyword matching, with alias expansion (`mid` → `regular/middle/intermediate`, `senior` → `lead/principal/staff/architect`)
6. **Deduplication** — for each `JobId`, only the highest-scoring chunk is kept.
7. **Truncation** — results are ordered by score descending and limited to `ResultCount`.

## Services

| File | Purpose |
|------|---------|
| `RetrievalService.cs` | Orchestrates the full pipeline above |
| `JobFilterMatcher.cs` | Evaluates all structured filters against a `JobChunkDocument`; returns whether the document is a match and which filters were satisfied |
| `JobMetadataParser.cs` | Splits pipe-delimited metadata strings and infers seniority from text tokens |
| `QueryTextNormalizer.cs` | Canonicalises tech names and tokenises text for filter matching |
| `RetrievalHealthService.cs` | Ensures the vector collection exists on startup and handles `GET /health` |
| `RetrievalRequestValidator.cs` | Input validation for the `POST /retrieve` request body |

## Configuration

Set `Embedding:ApiKey` via user secrets or an environment variable — do not hard-code it in `appsettings.json`.

```bash
dotnet user-secrets set "Embedding:ApiKey" "sk-..."
```

Key settings in `appsettings.json`:

| Key | Default | Notes |
|-----|---------|-------|
| `Embedding:Model` | `text-embedding-3-small` | |
| `Embedding:Dimensions` | `1536` | Must match stored vectors |
| `Retrieval:CandidateCount` | `20` | How many vectors to fetch before filtering |
| `Retrieval:ResultCount` | `5` | Max matches in the response |
| `Retrieval:MinimumSemanticScore` | `0.35` | Cosine similarity cut-off |
| `ConnectionStrings:VectorDb` | `../artifacts/devbg-jobs-full.db` | Path resolved relative to `ContentRootPath` |

## Running

```bash
cd src/rag-server
dotnet user-secrets set "Embedding:ApiKey" "sk-..."
dotnet run
```

The server binds to `http://localhost:5100` by default (configured via `Urls` in `appsettings.json`).
