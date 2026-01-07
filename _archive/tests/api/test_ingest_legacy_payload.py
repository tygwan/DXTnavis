"""
Test backward compatibility with legacy unique_id field (Phase B)

Tests verify that the ingest endpoint:
1. Accepts legacy payloads with unique_id instead of unique_key
2. Automatically promotes unique_id to unique_key
3. Extracts object_guid from UUID-like unique_id values
"""
import pytest
import pytest_asyncio
from httpx import AsyncClient


@pytest_asyncio.fixture
async def api_client():
    """Create a test HTTP client for FastAPI app."""
    from fastapi_server.main import app
    from fastapi_server.database import db
    from httpx import ASGITransport

    # Initialize database pool for the app
    await db.connect_pool()

    try:
        async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
            yield client
    finally:
        await db.close_pool()


@pytest.mark.asyncio
async def test_legacy_unique_id_promoted_to_unique_key(api_client: AsyncClient, db_pool):
    """
    Test that legacy unique_id is promoted to unique_key.

    Backward compatibility requirement from techspec.md §2.2:
    - If payload has unique_id but not unique_key, promote it
    - If unique_id is a valid UUID, also set object_guid
    """
    # Legacy payload with unique_id instead of unique_key
    legacy_payload = {
        "project_code": "TEST_LEGACY",
        "revision_number": 1,
        "source_type": "revit",
        "objects": [
            {
                "unique_id": "a1b2c3d4-e5f6-7890-abcd-1234567890ab",  # UUID format
                "category": "Walls",
                "name": "Legacy-Wall-001",
                "properties": {"Type": "Basic"},
                "source_type": "revit"
            }
        ]
    }

    response = await api_client.post("/api/v1/ingest", json=legacy_payload)
    assert response.status_code == 200, f"Legacy ingest failed: {response.text}"

    # Verify unique_key was populated from unique_id
    async with db_pool.acquire() as conn:
        row = await conn.fetchrow(
            """
            SELECT unique_key, object_guid
            FROM unified_objects
            WHERE unique_key = $1
            """,
            "a1b2c3d4-e5f6-7890-abcd-1234567890ab"
        )
        assert row is not None, "Object not created from legacy payload"
        assert row["unique_key"] == "a1b2c3d4-e5f6-7890-abcd-1234567890ab"
        assert str(row["object_guid"]) == "a1b2c3d4-e5f6-7890-abcd-1234567890ab", \
            "object_guid should be populated from UUID-like unique_id"


@pytest.mark.asyncio
async def test_legacy_non_uuid_unique_id(api_client: AsyncClient, db_pool):
    """
    Test that non-UUID unique_id values are handled correctly.

    When unique_id is not a valid UUID format:
    - unique_key = unique_id (preserved)
    - object_guid = None (no valid GUID to extract)
    """
    legacy_payload = {
        "project_code": "TEST_LEGACY_NON_UUID",
        "revision_number": 1,
        "source_type": "revit",
        "objects": [
            {
                "unique_id": "custom-id-wall-basement-001",  # Not a UUID
                "category": "Walls",
                "name": "Custom-ID-Wall",
                "properties": {},
                "source_type": "revit"
            }
        ]
    }

    response = await api_client.post("/api/v1/ingest", json=legacy_payload)
    assert response.status_code == 200, f"Legacy non-UUID ingest failed: {response.text}"

    async with db_pool.acquire() as conn:
        row = await conn.fetchrow(
            """
            SELECT unique_key, object_guid
            FROM unified_objects
            WHERE unique_key = $1
            """,
            "custom-id-wall-basement-001"
        )
        assert row is not None, "Object not created"
        assert row["unique_key"] == "custom-id-wall-basement-001"
        # object_guid should be None since unique_id is not a UUID
        assert row["object_guid"] is None or row["object_guid"] == "", \
            "object_guid should be None for non-UUID unique_id"


@pytest.mark.asyncio
async def test_modern_payload_with_both_fields(api_client: AsyncClient, db_pool):
    """
    Test that modern payloads with both unique_key and object_guid work correctly.

    Modern DXrevit clients should send both fields explicitly.
    """
    modern_payload = {
        "project_code": "TEST_MODERN",
        "revision_number": 1,
        "source_type": "revit",
        "objects": [
            {
                "unique_key": "wall-foundation-N123-E456",  # Semantic ID
                "object_guid": "f1e2d3c4-b5a6-7890-abcd-0987654321fe",  # Extracted GUID
                "category": "Structural Foundations",
                "name": "Foundation-Wall-001",
                "properties": {"Thickness": "300"},
                "source_type": "revit"
            }
        ]
    }

    response = await api_client.post("/api/v1/ingest", json=modern_payload)
    assert response.status_code == 200, f"Modern ingest failed: {response.text}"

    async with db_pool.acquire() as conn:
        row = await conn.fetchrow(
            """
            SELECT unique_key, object_guid
            FROM unified_objects
            WHERE unique_key = $1
            """,
            "wall-foundation-N123-E456"
        )
        assert row is not None, "Object not created"
        assert row["unique_key"] == "wall-foundation-N123-E456"
        assert str(row["object_guid"]) == "f1e2d3c4-b5a6-7890-abcd-0987654321fe"


@pytest.mark.asyncio
async def test_mixed_legacy_and_modern_objects(api_client: AsyncClient, db_pool):
    """
    Test that a single batch can contain both legacy and modern format objects.

    This tests transition period where some objects might be old format.
    """
    mixed_payload = {
        "project_code": "TEST_MIXED",
        "revision_number": 1,
        "source_type": "revit",
        "objects": [
            # Legacy format (unique_id only)
            {
                "unique_id": "legacy-wall-001",
                "category": "Walls",
                "name": "Legacy-Wall",
                "properties": {},
                "source_type": "revit"
            },
            # Modern format (unique_key + object_guid)
            {
                "unique_key": "modern-wall-001",
                "object_guid": "aaaabbbb-cccc-dddd-eeee-111122223333",
                "category": "Walls",
                "name": "Modern-Wall",
                "properties": {},
                "source_type": "revit"
            }
        ]
    }

    response = await api_client.post("/api/v1/ingest", json=mixed_payload)
    assert response.status_code == 200, f"Mixed batch ingest failed: {response.text}"

    # Verify both objects were created
    async with db_pool.acquire() as conn:
        count = await conn.fetchval(
            """
            SELECT COUNT(*) FROM unified_objects uo
            JOIN revisions r ON r.id = uo.revision_id
            JOIN projects p ON p.id = r.project_id
            WHERE p.code = $1
            """,
            "TEST_MIXED"
        )
        assert count == 2, f"Expected 2 objects, got {count}"

        # Verify legacy object
        legacy_row = await conn.fetchrow(
            "SELECT unique_key, object_guid FROM unified_objects WHERE unique_key = $1",
            "legacy-wall-001"
        )
        assert legacy_row is not None, "Legacy object not created"

        # Verify modern object
        modern_row = await conn.fetchrow(
            "SELECT unique_key, object_guid FROM unified_objects WHERE unique_key = $1",
            "modern-wall-001"
        )
        assert modern_row is not None, "Modern object not created"
        assert str(modern_row["object_guid"]) == "aaaabbbb-cccc-dddd-eeee-111122223333"
