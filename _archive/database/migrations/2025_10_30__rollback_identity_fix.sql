-- Phase A-4: Rollback Script for Identity Fix
-- Purpose: Revert dual-identity changes if needed
-- Related: techspec.md §1.5
-- Author: AWP 2025 Development Team
-- Date: 2025-10-30
-- WARNING: This will DESTROY data - use only in emergency

BEGIN;

-- Drop views created in views_latest migration
DROP VIEW IF EXISTS v_unified_objects_latest;
DROP VIEW IF EXISTS v_latest_revisions;

-- Drop indexes created in indexes migration
DROP INDEX IF EXISTS idx_unified_objects_object_guid;
DROP INDEX IF EXISTS idx_unified_objects_unique_key;
DROP INDEX IF EXISTS idx_unified_objects_properties;
DROP INDEX IF EXISTS idx_unified_objects_category;
DROP INDEX IF EXISTS idx_unified_objects_revision_id;

-- Remove new constraint
ALTER TABLE unified_objects
  DROP CONSTRAINT IF EXISTS uq_unified_object_by_unique_key;

-- Revert column name change (object_guid -> object_id)
ALTER TABLE unified_objects
  RENAME COLUMN object_guid TO object_id;

-- Remove unique_key column
ALTER TABLE unified_objects
  DROP COLUMN IF EXISTS unique_key;

-- Restore original constraint if it existed
-- Note: Original schema may not have had this constraint

-- Optional: Remove supporting columns if needed
-- Uncomment these lines only if you want to fully revert to original state
-- ALTER TABLE unified_objects DROP COLUMN IF EXISTS updated_at;
-- ALTER TABLE unified_objects DROP COLUMN IF EXISTS geometry;

COMMIT;

-- Verification Query (run separately):
-- SELECT column_name FROM information_schema.columns WHERE table_name='unified_objects' ORDER BY column_name;
