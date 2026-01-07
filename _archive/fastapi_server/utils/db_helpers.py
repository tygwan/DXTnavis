"""
Database helper utilities for project and revision management.

Phase B requirement:
- Automatic project creation if not exists
- Automatic revision creation if not exists
- Transaction-safe get-or-create operations
"""

import asyncpg
from typing import Tuple
from uuid import uuid4


async def get_or_create_project(
    conn: asyncpg.Connection,
    project_code: str,
    project_name: str = None,
    created_by: str = "system"
) -> Tuple[str, bool]:
    """
    Get existing project by code or create new one.

    Args:
        conn: Database connection
        project_code: Unique project code
        project_name: Project name (defaults to project_code if not provided)
        created_by: Creator identifier

    Returns:
        Tuple of (project_id: str, created: bool)
        - project_id: UUID of the project
        - created: True if new project was created, False if existing
    """
    # Try to get existing project
    row = await conn.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code
    )

    if row:
        return str(row["id"]), False

    # Project doesn't exist, create it
    if project_name is None:
        project_name = project_code

    project_id = await conn.fetchval(
        """
        INSERT INTO projects (id, code, name, created_by)
        VALUES ($1, $2, $3, $4)
        ON CONFLICT (code) DO UPDATE SET code = EXCLUDED.code
        RETURNING id
        """,
        uuid4(),
        project_code,
        project_name,
        created_by
    )

    return str(project_id), True


async def get_or_create_revision(
    conn: asyncpg.Connection,
    project_id: str,
    revision_number: int,
    source_type: str = "revit",
    description: str = None
) -> Tuple[str, bool]:
    """
    Get existing revision or create new one.

    Args:
        conn: Database connection
        project_id: Project UUID
        revision_number: Revision number (1-based)
        source_type: Source type (revit, navisworks, ifc)
        description: Optional revision description

    Returns:
        Tuple of (revision_id: str, created: bool)
        - revision_id: UUID of the revision
        - created: True if new revision was created, False if existing
    """
    # Try to get existing revision
    row = await conn.fetchrow(
        """
        SELECT id FROM revisions
        WHERE project_id = $1 AND revision_number = $2
        """,
        project_id,
        revision_number
    )

    if row:
        return str(row["id"]), False

    # Revision doesn't exist, create it
    revision_id = await conn.fetchval(
        """
        INSERT INTO revisions (id, project_id, revision_number, source_type, description)
        VALUES ($1, $2, $3, $4, $5)
        ON CONFLICT (project_id, revision_number) DO UPDATE SET revision_number = EXCLUDED.revision_number
        RETURNING id
        """,
        uuid4(),
        project_id,
        revision_number,
        source_type,
        description
    )

    return str(revision_id), True


async def ensure_project_and_revision(
    conn: asyncpg.Connection,
    project_code: str,
    revision_number: int,
    source_type: str = "revit"
) -> Tuple[str, str]:
    """
    Ensure both project and revision exist, creating them if necessary.

    This is a convenience function that combines get_or_create_project and
    get_or_create_revision into a single transaction-safe operation.

    Args:
        conn: Database connection
        project_code: Project code
        revision_number: Revision number
        source_type: Source type (revit, navisworks, ifc)

    Returns:
        Tuple of (project_id: str, revision_id: str)
    """
    # Get or create project
    project_id, _ = await get_or_create_project(conn, project_code)

    # Get or create revision
    revision_id, _ = await get_or_create_revision(
        conn,
        project_id,
        revision_number,
        source_type
    )

    return project_id, revision_id
