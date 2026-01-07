# DX Platform Data Pipeline (Docs → JSON → PostgreSQL → RAG → Visualization)

## Goals
- Unified pipeline from document parsing to analytics and RAG.
- Consistent JSON schemas, durable storage, and reproducible indexing.
- Clear API contracts and roles for ingestion, query, and visualization.

## End-to-End Flow
```
[Sources: PDFs, DOCX, XLSX, HTML, CSV, Revit exports]
        ↓ (parsers, normalizers)
  Document JSON (Documents, Chunks, Relationships)
        ↓ (ingest API)
     PostgreSQL (raw JSONB + metadata)
        ↓ (embed)
     pgvector (chunk embeddings)
        ↓ (retrieve)
     RAG service (retriever + LLM)
        ↓ (serve)
     Analytics APIs + Views
        ↓ (consume)
     Dashboards (Power BI / Grafana / Superset)
```

## Components
- Parsers: Python-based (e.g., `unstructured`, `pymupdf`, `python-docx`, `pandas` for CSV). Output normalized JSON.
- Ingestion API: FastAPI endpoints to upsert documents/chunks and kick off embedding jobs.
- Storage: PostgreSQL with JSONB columns and `pgvector` for embeddings.
- RAG Service: Retriever (KNN over `pgvector`) + prompt templates + optional chat session logging.
- Visualization: Read-only role uses analytics views to power BI tools.

## JSON Schemas (ingestion)
- DocumentRecord:
```
{
  "documentId": "uuid-or-stable-id",
  "source": "manual|s3|sharepoint|git|revit",
  "project": "Project A",
  "title": "Spec Section 01",
  "uri": "file:///..." ,
  "contentType": "pdf|docx|html|csv|json",
  "checksum": "sha256-...",
  "createdAt": "2025-10-13T09:00:00Z",
  "metadata": { "discipline": "ARCH", "version": "v1.2" }
}
```
- ChunkRecord:
```
{
  "chunkId": "uuid",
  "documentId": "uuid-or-stable-id",
  "index": 0,
  "text": "normalized text...",
  "tokens": 235,
  "metadata": { "page": 3, "section": "1.2" }
}
```
- RelationshipRecord (optional):
```
{
  "documentId": "uuid",
  "sourceChunkId": "uuid",
  "targetChunkId": "uuid",
  "relationType": "references|duplicates|supersedes"
}
```

## API Design (FastAPI)
- `POST /api/docs` — upsert documents (batch allowed).
- `POST /api/chunks` — upsert chunks (batch allowed).
- `POST /api/embeddings/reindex?documentId=...` — enqueue embedding for document/chunks.
- `POST /api/rag/query` — body: { query, project?, topK?, filters? } → returns answers + citations.
- `GET /api/analytics/rag/usage?from=&to=` — usage metrics for dashboards.

## Database Model (PostgreSQL)
- `knowledge_sources` — registry of source systems.
- `documents` — one row per ingested document; JSONB `metadata` for flexibility.
- `document_chunks` — normalized text slices; JSONB `metadata` for page/section.
- `chunk_embeddings` — `vector` column for embeddings; one row per chunk per model.
- `rag_qa_sessions` & `rag_qa_interactions` — optional chat session and per-turn logs.

See SQL files under `database/extensions` and `database/tables`.

## Embedding
- Use `pgvector` (`CREATE EXTENSION IF NOT EXISTS vector;`).
- Example dims: 768 (mini) or 1536 (OpenAI text-embedding-3-small/large). Store `embedding_dim` and `model` per row.
- KNN query pattern:
```
SELECT c.chunk_id, c.document_id, ce.model, c.text, ce.embedding <=> $1 AS distance
FROM chunk_embeddings ce
JOIN document_chunks c ON c.chunk_id = ce.chunk_id
WHERE ce.model = $2 AND (c.metadata->>'project') = $3
ORDER BY ce.embedding <=> $1
LIMIT $4;
```

## Roles & Security
- `dx_api_role`: RW on raw tables; executes embedding jobs.
- `dx_readonly_role`: R on analytics/views only; used by BI tools.
- PII control via dedicated `metadata` flags and filtered views.

## Visualization
- Preferred: Power BI DirectQuery, Grafana (Postgres), or Apache Superset.
- Ship views for stable schemas, e.g., `analytics_version_summary`, `analytics_4d_link_data`, and `analytics_rag_usage` (added).
- Filters: project, version, source, time window.

## Backfill & Operations
- Backfill: parse → upsert → reindex.
- Idempotency: use `ON CONFLICT ... DO UPDATE` by `document_id` and `chunk_id`.
- Monitoring: log counts, tokens, and embedding success/failure by model.

## Testing Checklist
- Ingest a sample PDF and CSV, verify counts in tables.
- Run `reindex`, validate `chunk_embeddings` row counts and KNN query results.
- Validate `analytics_rag_usage` returns data for the time window.

## Next Steps
- Implement routers: `/api/docs`, `/api/chunks`, `/api/embeddings/reindex`, `/api/rag/query`.
- Add background worker (Celery/RQ) or FastAPI BackgroundTasks for embedding.
- Configure BI connection using `dx_readonly_role`.

