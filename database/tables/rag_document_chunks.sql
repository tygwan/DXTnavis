-- ========================================
-- rag_document_chunks: normalized text chunks per document
-- ========================================

CREATE TABLE IF NOT EXISTS rag_document_chunks (
    chunk_id VARCHAR(255) PRIMARY KEY,
    document_id VARCHAR(255) NOT NULL REFERENCES rag_documents(document_id) ON DELETE CASCADE,
    chunk_index INTEGER NOT NULL,
    text TEXT NOT NULL,
    tokens INTEGER,
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_rag_chunks_doc_index UNIQUE (document_id, chunk_index)
);

CREATE INDEX IF NOT EXISTS idx_rag_chunks_document_id ON rag_document_chunks(document_id);
CREATE INDEX IF NOT EXISTS idx_rag_chunks_metadata_gin ON rag_document_chunks USING GIN (metadata);

COMMENT ON TABLE rag_document_chunks IS 'Normalized text chunks derived from documents.';

