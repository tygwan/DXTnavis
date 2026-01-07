-- Phase B: Make object_guid Nullable Migration
-- Purpose: Allow NULL values for object_guid to support objects without valid UUIDs
-- Rationale: Not all BIM objects have a valid UUID (e.g., annotations, dimensions, non-IFC elements)
-- Related: techspec.md §2.1 - Dual-identity pattern with optional object_guid
-- Author: AWP 2025 Development Team
-- Date: 2025-10-30

BEGIN;

-- Make object_guid nullable to support objects without valid UUIDs
ALTER TABLE unified_objects
  ALTER COLUMN object_guid DROP NOT NULL;

-- Verification: Check that object_guid is now nullable
-- Expected: is_nullable = 'YES' for object_guid column

COMMIT;

-- Verification Query (run separately):
-- SELECT column_name, is_nullable
-- FROM information_schema.columns
-- WHERE table_name='unified_objects' AND column_name='object_guid';
