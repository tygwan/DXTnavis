"""
Database migration tests for Phase A - Database Layer

Tests verify that migration scripts correctly modify the schema
following the dual-identity pattern (unique_key, object_guid).
"""
import pytest
import asyncpg


@pytest.mark.asyncio
async def test_unified_objects_columns_after_identity_fix(db_pool: asyncpg.Pool):
    """
    Test that unified_objects table has required columns after identity_fix migration.

    Expected columns:
    - unique_key: TEXT - Original identifier preserving full UniqueId from source
    - object_guid: TEXT - Extracted standard UUID for matching
    - geometry: JSONB - Geometric data in JSON format
    - updated_at: TIMESTAMPTZ - Last update timestamp

    This test verifies Phase A-2 (identity_fix migration) completion.
    """
    async with db_pool.acquire() as conn:
        rows = await conn.fetch("""
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'unified_objects'
            ORDER BY column_name
        """)

        # Extract column names into a set for easy checking
        column_names = {row["column_name"] for row in rows}

        # Required columns from plan.md Phase A-2
        required_columns = {"unique_key", "object_guid", "geometry", "updated_at"}

        # Verify each required column exists
        for expected_column in required_columns:
            assert expected_column in column_names, (
                f"Missing required column: {expected_column}. "
                f"Found columns: {sorted(column_names)}"
            )


@pytest.mark.asyncio
async def test_base_prereqs_extensions_exist(db_pool: asyncpg.Pool):
    """
    Test that required PostgreSQL extensions are installed.

    Expected extensions from Phase A-1 (base_prereqs):
    - pgcrypto: For gen_random_uuid()
    - pg_trgm: For trigram text search
    """
    async with db_pool.acquire() as conn:
        rows = await conn.fetch("""
            SELECT extname
            FROM pg_extension
            WHERE extname IN ('pgcrypto', 'pg_trgm')
        """)

        extension_names = {row["extname"] for row in rows}

        assert "pgcrypto" in extension_names, "pgcrypto extension not installed"
        assert "pg_trgm" in extension_names, "pg_trgm extension not installed"


@pytest.mark.asyncio
async def test_projects_unique_constraint_exists(db_pool: asyncpg.Pool):
    """
    Test that projects.code has UNIQUE constraint.

    This ensures project codes are unique across the system.
    Required by Phase A-1 (base_prereqs).
    """
    async with db_pool.acquire() as conn:
        rows = await conn.fetch("""
            SELECT constraint_name
            FROM information_schema.table_constraints
            WHERE table_name = 'projects'
              AND constraint_type = 'UNIQUE'
              AND constraint_name = 'uq_projects_code'
        """)

        assert len(rows) > 0, "Missing UNIQUE constraint on projects.code"


@pytest.mark.asyncio
async def test_revisions_unique_constraint_exists(db_pool: asyncpg.Pool):
    """
    Test that revisions has UNIQUE constraint on (project_id, revision_number, source_type).

    This ensures each project-revision-source combination is unique.
    Required by Phase A-1 (base_prereqs).
    """
    async with db_pool.acquire() as conn:
        rows = await conn.fetch("""
            SELECT constraint_name
            FROM information_schema.table_constraints
            WHERE table_name = 'revisions'
              AND constraint_type = 'UNIQUE'
              AND constraint_name = 'uq_revisions'
        """)

        assert len(rows) > 0, (
            "Missing UNIQUE constraint on revisions "
            "(project_id, revision_number, source_type)"
        )
