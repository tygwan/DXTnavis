-- ========================================
-- Schema patch: unify Revit & Navisworks revisions
-- ========================================

BEGIN;

-- Link revisions to DXrevit model versions
ALTER TABLE revisions
    ADD COLUMN IF NOT EXISTS model_version VARCHAR(255);

CREATE UNIQUE INDEX IF NOT EXISTS uq_revisions_model_version_idx
    ON revisions(model_version)
    WHERE model_version IS NOT NULL;

-- Elevate IFC GUIDs and Navisworks references onto unified objects
ALTER TABLE unified_objects
    ADD COLUMN IF NOT EXISTS ifc_guid VARCHAR(64),
    ADD COLUMN IF NOT EXISTS navisworks_object_id UUID;

CREATE INDEX IF NOT EXISTS idx_unified_ifc_guid
    ON unified_objects(ifc_guid);

-- Extend Navisworks hierarchy with revision linkage
ALTER TABLE navisworks_hierarchy
    ADD COLUMN IF NOT EXISTS project_id UUID REFERENCES projects(id) ON DELETE CASCADE,
    ADD COLUMN IF NOT EXISTS revision_id UUID REFERENCES revisions(id) ON DELETE CASCADE,
    ADD COLUMN IF NOT EXISTS ifc_guid VARCHAR(64),
    ADD COLUMN IF NOT EXISTS source_file_path TEXT;

CREATE INDEX IF NOT EXISTS idx_hierarchy_project
    ON navisworks_hierarchy(project_id);

CREATE INDEX IF NOT EXISTS idx_hierarchy_revision
    ON navisworks_hierarchy(revision_id);

CREATE INDEX IF NOT EXISTS idx_hierarchy_ifc_guid
    ON navisworks_hierarchy(ifc_guid);

-- Refresh aggregated view with new columns
DROP VIEW IF EXISTS v_hierarchy_objects;

CREATE VIEW v_hierarchy_objects AS
SELECT
    object_id,
    parent_id,
    level,
    display_name,
    model_version,
    project_id,
    revision_id,
    ifc_guid,
    jsonb_object_agg(property_name, property_value)
        FILTER (WHERE property_name IS NOT NULL) AS properties,
    MIN(created_at) AS created_at
FROM navisworks_hierarchy
GROUP BY object_id, parent_id, level, display_name, model_version, project_id, revision_id, ifc_guid;

COMMENT ON VIEW v_hierarchy_objects IS
    'Navisworks 계층 EAV 데이터를 객체 단위로 집계한 뷰 (프로젝트/리비전/IFC GUID 포함).';

-- Map source-specific versions to revisions (Revit/Navisworks parity)
CREATE TABLE IF NOT EXISTS revision_versions (
    model_version VARCHAR(255) PRIMARY KEY,
    revision_id UUID NOT NULL REFERENCES revisions(id) ON DELETE CASCADE,
    source_type VARCHAR(20) NOT NULL CHECK (source_type IN ('revit', 'navisworks', 'both')),
    extracted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    source_file_path TEXT
);

CREATE INDEX IF NOT EXISTS idx_revision_versions_revision
    ON revision_versions(revision_id);

CREATE INDEX IF NOT EXISTS idx_revision_versions_source_type
    ON revision_versions(source_type);

COMMIT;
