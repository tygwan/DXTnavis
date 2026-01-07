-- Phase A-2: Unified Objects Identity Fix Migration
-- Purpose: Implement dual-identity pattern (unique_key, object_guid)
-- Related: techspec.md §1.2
-- Author: AWP 2025 Development Team
-- Date: 2025-10-30
-- Related Test: tests/db/test_migrations.py::test_unified_objects_columns_after_identity_fix

BEGIN;

-- Add unique_key column for original identifier preservation
ALTER TABLE unified_objects
  ADD COLUMN IF NOT EXISTS unique_key TEXT;

-- Rename existing object_id to object_guid for semantic clarity
-- This aligns with the dual-identity pattern (unique_key, object_guid)
DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_name='unified_objects' AND column_name='object_id'
  ) AND NOT EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_name='unified_objects' AND column_name='object_guid'
  ) THEN
    ALTER TABLE unified_objects RENAME COLUMN object_id TO object_guid;
  END IF;
END $$;

-- Backfill unique_key from existing object_guid data
-- For legacy records, unique_key defaults to object_guid::text
-- If object_guid is NULL, generate a unique key using id
UPDATE unified_objects
   SET unique_key = COALESCE(
     unique_key,
     CASE
       WHEN object_guid IS NOT NULL THEN object_guid::text
       ELSE 'legacy_' || id::text
     END
   )
 WHERE unique_key IS NULL;

-- Remove old constraint if exists
ALTER TABLE unified_objects
  DROP CONSTRAINT IF EXISTS uq_unified_object;

-- Add new constraint based on unique_key
ALTER TABLE unified_objects
  ADD CONSTRAINT uq_unified_object_by_unique_key
  UNIQUE (revision_id, source_type, unique_key);

COMMIT;

-- Verification Queries (run separately):
-- SELECT column_name FROM information_schema.columns WHERE table_name='unified_objects' ORDER BY column_name;
-- SELECT revision_id, source_type, unique_key, COUNT(*) FROM unified_objects GROUP BY 1,2,3 HAVING COUNT(*) > 1;
