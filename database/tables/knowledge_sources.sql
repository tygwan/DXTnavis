-- ========================================
-- knowledge_sources: registry of document sources
-- ========================================

CREATE TABLE IF NOT EXISTS knowledge_sources (
    source_id VARCHAR(100) PRIMARY KEY,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE knowledge_sources IS 'Registry of external knowledge/document sources (e.g., sharepoint, s3, git, manual, revit).';

