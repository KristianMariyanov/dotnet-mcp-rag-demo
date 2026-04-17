# seeder

Offline CLI that crawls the [dev.bg](https://dev.bg) job board, parses each listing, splits it into text chunks, generates embeddings, and upserts everything into a SQLite database. Run this once (or periodically) to populate or refresh the knowledge base that `rag-server` queries.

## What it does

1. **Discover** — fetches the dev.bg sitemap index and enumerates every job-listing URL.
2. **Skip unchanged** — compares each URL's `lastmod` from the sitemap against the value already stored in SQLite; unchanged jobs are not re-fetched.
3. **Fetch & parse** — downloads each job page and extracts: title, company, location, work model, employment type, categories, technologies, tags, description HTML/text, salary, and rich JSON-LD metadata.
4. **Chunk** — splits each job into retrieval-friendly text chunks:
   - **overview chunk** — a compact single chunk with all structured metadata concatenated
   - **description chunks** — the description HTML is parsed into logical blocks, then merged into ~900-character chunks; plain-text fallback is used when HTML structure is absent
5. **Embed** — each chunk is enriched with a `SearchText` field (metadata prepended to chunk text) and passed to `IEmbeddingGenerator` to produce a 1536-dimensional float vector.
6. **Upsert** — job rows, category/technology rows, and vector chunk rows are written to SQLite atomically. Stale jobs (removed from the sitemap) are deleted.
7. **Validate** — after seeding, counts in the database are compared against the live sitemap to confirm no URLs were missed.

## Usage

```bash
cd src/seeder
dotnet run -- [options]
```

### Options

| Flag | Default | Description |
|------|---------|-------------|
| `--database <path>` | `artifacts/devbg-jobs.db` | SQLite output file (created if absent) |
| `--sitemap-index-url <url>` | `https://dev.bg/wp-sitemap.xml` | Entry-point sitemap |
| `--concurrency <n>` | `8` | Parallel HTTP fetch workers |
| `--limit <n>` | *(none)* | Process only the first N URLs — useful for quick tests |
| `--validate-only` | `false` | Skip seeding; only compare DB counts against the sitemap |
| `--skip-validation` | `false` | Skip the post-seed validation step |
| `--help` / `-h` | | Show usage |

### Examples

Full seed run:

```bash
dotnet run -- --database ../../artifacts/devbg-jobs-full.db
```

Quick smoke test with 20 jobs:

```bash
dotnet run -- --database ../../artifacts/devbg-jobs-test.db --limit 20
```

Validate an existing database without re-scraping:

```bash
dotnet run -- --database ../../artifacts/devbg-jobs-full.db --validate-only
```

## Embedding configuration

Set credentials via **user secrets** (recommended for local dev) or environment variables. Do not hard-code keys.

### OpenAI

```bash
dotnet user-secrets set "Embedding:ApiKey" "sk-..."
```

Or via environment variable: `Embedding__ApiKey` / `OPENAI_API_KEY`.

### Azure OpenAI

```bash
dotnet user-secrets set "AzureOpenAI:ApiKey" "..."
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://..."
dotnet user-secrets set "AzureOpenAI:Deployment" "text-embedding-3-small"
```

The seeder prefers the plain OpenAI provider when `Embedding:ApiKey` is set; it falls back to Azure OpenAI when the Azure credentials are present instead.

The embedding model defaults to `text-embedding-3-small` and dimensions are fixed at **1536** to match the stored vector schema.

## Services

| File | Purpose |
|------|---------|
| `DevBgSeeder.cs` | Top-level orchestrator — discover, skip, fetch, index, validate |
| `DevBgSitemapClient.cs` | Fetches and parses the sitemap index and job sitemaps |
| `DevBgJobPageParser.cs` | Extracts structured metadata and description from raw HTML |
| `JobVectorIndexer.cs` | Calls `JobChunkBuilder` then embeds the resulting chunks |
| `Chunking/JobChunkBuilder.cs` | Splits a job posting into an overview chunk + description chunks |
| `SqliteJobStore.cs` | All SQLite reads and writes (jobs, categories, technologies, vector chunks) |
| `SeederValidationService.cs` | Post-seed comparison of DB counts vs. live sitemap |
| `HttpClientFactory.cs` | Creates the shared `HttpClient` with a sensible user-agent |
| `Support/TextNormalizer.cs` | Strips excess whitespace from parsed text |

## Output

The seeder prints progress to stdout:

```
Discovered 3421 job URL(s) from https://dev.bg/wp-sitemap.xml.
[1/120] Indexing 12345 | Senior .NET Engineer @ Acme Ltd
[1/120] Stored 4 vector chunk(s) for https://dev.bg/...
...
Seeded 120 job(s) from 3421 sitemap URL(s); 3301 unchanged job(s) skipped; 481 vector chunk(s) indexed with openai:text-embedding-3-small; 0 failed, 2 stale row(s) removed.

Validation: sitemap has 3421 job(s), database has 3421 job row(s), and 13842 vector chunk row(s).
Validation passed. Every sitemap job URL is present and every stored job has vector chunks.
SQLite database: ../../artifacts/devbg-jobs-full.db
```

Exit code `0` on success, `1` if any jobs failed or validation found discrepancies.
