-- ========================================
-- rag_chunk_embeddings: pgvector embeddings for chunks
-- ========================================

-- Ensure pgvector is available
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS rag_chunk_embeddings (
    embedding_id BIGSERIAL PRIMARY KEY,
    chunk_id VARCHAR(255) NOT NULL REFERENCES rag_document_chunks(chunk_id) ON DELETE CASCADE,
    model VARCHAR(100) NOT NULL,
    embedding_dim INTEGER NOT NULL,
    embedding VECTOR, -- set dimensionality via CHECK
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_rag_embedding_chunk_model UNIQUE (chunk_id, model)
);

-- Optional dimensionality guard (example 1536)
-- ALTER TABLE rag_chunk_embeddings
--   ADD CONSTRAINT chk_rag_embedding_dim CHECK (embedding IS NULL OR (vector_dims(embedding) = embedding_dim));

CREATE INDEX IF NOT EXISTS idx_rag_embeddings_chunk_id ON rag_chunk_embeddings(chunk_id);
CREATE INDEX IF NOT EXISTS idx_rag_embeddings_model ON rag_chunk_embeddings(model);
CREATE INDEX IF NOT EXISTS idx_rag_embeddings_ivfflat ON rag_chunk_embeddings USING ivfflat (embedding vector_l2_ops) WITH (lists = 100);

COMMENT ON TABLE rag_chunk_embeddings IS 'Vector embeddings per chunk and model for KNN retrieval.';

