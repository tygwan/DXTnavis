-- Phase A-1: Base Prerequisites Migration
-- Purpose: Install required extensions and add base constraints/columns
-- Author: AWP 2025 Development Team
-- Date: 2025-10-30
-- Related Test: tests/db/test_migrations.py::test_base_prereqs_extensions_exist

BEGIN;

-- Install required PostgreSQL extensions
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Ensure projects.code is unique
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'uq_projects_code'
  ) THEN
    ALTER TABLE projects ADD CONSTRAINT uq_projects_code UNIQUE (code);
  END IF;
END $$;

-- Ensure revisions combination is unique
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'uq_revisions'
  ) THEN
    ALTER TABLE revisions ADD CONSTRAINT uq_revisions
      UNIQUE (project_id, revision_number, source_type);
  END IF;
END $$;

-- Add supporting columns to unified_objects if not exist
ALTER TABLE unified_objects
  ADD COLUMN IF NOT EXISTS geometry JSONB,
  ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();

COMMIT;

-- Verification Query (run separately):
-- SELECT extname FROM pg_extension WHERE extname IN ('pgcrypto', 'pg_trgm');
-- SELECT column_name FROM information_schema.columns WHERE table_name = 'unified_objects';
