-- ========================================
-- rag_qa_sessions / rag_qa_interactions: optional RAG usage logging
-- ========================================

CREATE TABLE IF NOT EXISTS rag_qa_sessions (
    session_id VARCHAR(255) PRIMARY KEY,
    project VARCHAR(255),
    user_id VARCHAR(255),
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS rag_qa_interactions (
    id BIGSERIAL PRIMARY KEY,
    session_id VARCHAR(255) NOT NULL REFERENCES rag_qa_sessions(session_id) ON DELETE CASCADE,
    turn_index INTEGER NOT NULL,
    query TEXT NOT NULL,
    answer TEXT,
    citations JSONB,      -- [{documentId, chunkId, score}]
    metadata JSONB,       -- timing, token usage, model, etc.
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_rag_qa_turn UNIQUE (session_id, turn_index)
);

CREATE INDEX IF NOT EXISTS idx_rag_qa_session ON rag_qa_interactions(session_id);
CREATE INDEX IF NOT EXISTS idx_rag_qa_created_at ON rag_qa_interactions(created_at);

COMMENT ON TABLE rag_qa_sessions IS 'RAG chat sessions (for analytics).';
COMMENT ON TABLE rag_qa_interactions IS 'Per-turn logs including citations and metrics.';

