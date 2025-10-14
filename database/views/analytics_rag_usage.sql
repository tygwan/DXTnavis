-- ========================================
-- analytics_rag_usage: summarizes RAG traffic by day, project, model
-- ========================================

CREATE OR REPLACE VIEW analytics_rag_usage AS
SELECT
    date_trunc('day', i.created_at) AS day,
    s.project,
    COALESCE((i.metadata->>'model'), 'unknown') AS model,
    COUNT(*) AS turns,
    SUM(COALESCE((i.metadata->>'prompt_tokens')::INT, 0)) AS prompt_tokens,
    SUM(COALESCE((i.metadata->>'completion_tokens')::INT, 0)) AS completion_tokens
FROM rag_qa_interactions i
LEFT JOIN rag_qa_sessions s ON s.session_id = i.session_id
GROUP BY 1, 2, 3
ORDER BY 1 DESC, 2, 3;

COMMENT ON VIEW analytics_rag_usage IS 'Daily RAG usage metrics by project and model.';

