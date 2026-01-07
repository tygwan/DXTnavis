-- Phase A-3: Unified Objects Indexes Migration
-- Purpose: Create performance indexes for detection and query optimization
-- Related: techspec.md §1.3
-- Author: AWP 2025 Development Team
-- Date: 2025-10-30
-- Note: Uses CONCURRENTLY to avoid blocking production operations

-- Category index for filtering
DROP INDEX IF EXISTS idx_unified_objects_category;
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_category
  ON unified_objects(category);

-- JSONB properties index for key-value queries
DROP INDEX IF EXISTS idx_unified_objects_properties;
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_properties
  ON unified_objects USING GIN (properties);

-- Detection optimization indexes
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_unique_key
  ON unified_objects(unique_key);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_object_guid
  ON unified_objects(object_guid);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_revision_id
  ON unified_objects(revision_id);

-- Verification Query (run separately):
-- SELECT indexname, indexdef FROM pg_indexes WHERE tablename = 'unified_objects' ORDER BY indexname;
