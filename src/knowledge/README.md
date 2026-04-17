# knowledge

Shared class library referenced by both `seeder` and `rag-server`. It defines the vector document schema and the constants that must stay in sync between the indexing and retrieval sides.

## Why a shared library?

The `JobChunkDocument` class is decorated with `Microsoft.Extensions.VectorData` attributes (`[VectorStoreKey]`, `[VectorStoreData]`, `[VectorStoreVector]`). Both the seeder (which writes documents) and the rag-server (which reads them) must see the same property names, types, and vector dimensions. Putting this class in one shared project prevents the two projects drifting out of sync.

## Contents

### `Data/JobChunkDocument.cs`

The document stored in, and retrieved from, the SQLite vector collection. Each record represents one text chunk derived from a single job posting.

| Property | Role |
|----------|------|
| `ChunkId` | Vector store key — `"{sourceJobId}_{chunkIndex}"` |
| `JobId` | Identifies the source job; used for deduplication during retrieval |
| `Url` | Canonical URL of the job listing |
| `Title` | Job title |
| `Company` | Company name |
| `Location` | City or region |
| `WorkModel` | `Remote`, `Hybrid`, `On-site`, etc. |
| `EmploymentType` | `Full-time`, `Part-time`, etc. |
| `Categories` | Pipe-separated category labels (e.g. `Backend\|DevOps`) |
| `Technologies` | Pipe-separated technology labels (e.g. `C#\|.NET\|Docker`) |
| `Tags` | Pipe-separated free-form tags |
| `Section` | `overview` for the summary chunk, or a description section heading |
| `ChunkText` | The raw text that was embedded — passed back to the model as grounded context |
| `SearchText` | Enriched search text used during embedding (metadata prepended to chunk text) |
| `PostedOn` | Date the job was posted |
| `IndexedAtUtc` | Timestamp when the chunk was written to the store |
| `Embedding` | 1536-dimensional float vector (`CosineDistance`) |

### `Data/KnowledgeConstants.cs`

Two constants shared across projects:

- `DefaultEmbeddingDimensions` — `1536`, matching `text-embedding-3-small`
- `DefaultVectorCollectionName` — `job_chunk_vectors`, the SQLite virtual table name
