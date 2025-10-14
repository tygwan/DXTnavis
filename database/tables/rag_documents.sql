-- ========================================
-- rag_documents: ingested documents
-- ========================================

CREATE TABLE IF NOT EXISTS rag_documents (
    document_id VARCHAR(255) PRIMARY KEY,
    source_id VARCHAR(100) REFERENCES knowledge_sources(source_id) ON DELETE SET NULL,
    project VARCHAR(255),
    title TEXT,
    uri TEXT,
    content_type VARCHAR(50),
    checksum VARCHAR(128),
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_rag_documents_project ON rag_documents(project);
CREATE INDEX IF NOT EXISTS idx_rag_documents_content_type ON rag_documents(content_type);
CREATE INDEX IF NOT EXISTS idx_rag_documents_metadata_gin ON rag_documents USING GIN (metadata);

COMMENT ON TABLE rag_documents IS 'Ingested documents for RAG and analytics.';

