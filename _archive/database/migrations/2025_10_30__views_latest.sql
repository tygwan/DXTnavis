-- Phase A-3: Latest Revision Views Migration
-- Purpose: Create views for querying latest revisions only
-- Related: techspec.md §1.4
-- Author: AWP 2025 Development Team
-- Date: 2025-10-30
-- Related Test: tests/db/test_latest_view.py::test_latest_revision_view_only_returns_max_revision

BEGIN;

-- View to identify the latest revision for each project
CREATE OR REPLACE VIEW v_latest_revisions AS
SELECT r.project_id, MAX(r.revision_number) AS latest_revision
  FROM revisions r
 GROUP BY r.project_id;

-- View to query only objects from the latest revision
-- This is used by the detection API to ensure accurate confidence calculations
CREATE OR REPLACE VIEW v_unified_objects_latest AS
SELECT uo.*
  FROM unified_objects uo
  JOIN revisions r ON r.id = uo.revision_id
  JOIN v_latest_revisions v
    ON v.project_id = r.project_id
   AND v.latest_revision = r.revision_number;

COMMIT;

-- Verification Queries (run separately):
-- SELECT * FROM v_latest_revisions LIMIT 10;
-- SELECT COUNT(*) FROM v_unified_objects_latest;
