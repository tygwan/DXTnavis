"""
Test ingest endpoint with new dual-identity schema (Phase B)

Tests verify that the /api/v1/ingest endpoint correctly handles:
1. New UnifiedObjectDto schema with unique_key and object_guid
2. Upsert behavior using unique_key as primary identifier
3. No duplicate records on repeated ingestion
"""
import pytest
import pytest_asyncio
from httpx import AsyncClient
from fastapi import FastAPI


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
async def test_ingest_upserts_by_unique_key(api_client: AsyncClient, db_pool):
    """
    Test that ingesting the same unique_key twice does not create duplicates.

    Phase B requirement: ON CONFLICT (revision_id, source_type, unique_key) DO UPDATE

    Steps:
    1. Ingest an object with a specific unique_key
    2. Ingest the same unique_key again with different properties
    3. Verify only one record exists
    4. Verify properties were merged
    """
    # Prepare test data with new schema
    payload = {
        "project_code": "TEST_UPSERT",
        "revision_number": 1,
        "source_type": "revit",
        "objects": [
            {
                "unique_key": "test-unique-key-001",
                "object_guid": "a1b2c3d4-e5f6-7890-abcd-1234567890ab",
                "category": "Walls",
                "name": "Wall-001",
                "properties": {
                    "Height": "3000",
                    "Width": "200"
                },
                "source_type": "revit"
            }
        ]
    }

    # First ingestion
    response1 = await api_client.post("/api/v1/ingest", json=payload)
    assert response1.status_code == 200, f"First ingest failed: {response1.text}"
    result1 = response1.json()
    assert result1["success"] is True
    revision_id = result1["revision_id"]

    # Count objects after first ingest
    async with db_pool.acquire() as conn:
        count1 = await conn.fetchval(
            "SELECT COUNT(*) FROM unified_objects WHERE unique_key = $1",
            "test-unique-key-001"
        )
        assert count1 == 1, f"Expected 1 object after first ingest, got {count1}"

    # Second ingestion with updated properties
    payload["objects"][0]["properties"]["Material"] = "Concrete"
    payload["objects"][0]["name"] = "Wall-001-Updated"

    response2 = await api_client.post("/api/v1/ingest", json=payload)
    assert response2.status_code == 200, f"Second ingest failed: {response2.text}"

    # Verify still only one record exists
    async with db_pool.acquire() as conn:
        count2 = await conn.fetchval(
            "SELECT COUNT(*) FROM unified_objects WHERE unique_key = $1",
            "test-unique-key-001"
        )
        assert count2 == 1, f"Expected 1 object after upsert, got {count2} (duplicates created!)"

        # Verify properties were merged
        row = await conn.fetchrow(
            "SELECT display_name, properties FROM unified_objects WHERE unique_key = $1",
            "test-unique-key-001"
        )
        assert row["display_name"] == "Wall-001-Updated", "Name not updated"
        assert "Height" in row["properties"], "Original property lost"
        assert "Material" in row["properties"], "New property not added"


@pytest.mark.asyncio
async def test_ingest_handles_null_object_guid(api_client: AsyncClient, db_pool):
    """
    Test that objects without object_guid are handled gracefully.

    Some objects may not have a valid GUID (e.g., non-IFC elements).
    unique_key should still work as the primary identifier.
    """
    payload = {
        "project_code": "TEST_NULL_GUID",
        "revision_number": 1,
        "source_type": "revit",
        "objects": [
            {
                "unique_key": "test-no-guid-001",
                "object_guid": None,  # NULL GUID
                "category": "Annotations",
                "name": "Dimension-001",
                "properties": {},
                "source_type": "revit"
            }
        ]
    }

    response = await api_client.post("/api/v1/ingest", json=payload)
    assert response.status_code == 200, f"Ingest failed: {response.text}"

    # Verify object was created with NULL object_guid
    async with db_pool.acquire() as conn:
        row = await conn.fetchrow(
            "SELECT unique_key, object_guid FROM unified_objects WHERE unique_key = $1",
            "test-no-guid-001"
        )
        assert row is not None, "Object not created"
        assert row["unique_key"] == "test-no-guid-001"
        assert row["object_guid"] is None, "object_guid should be NULL"


@pytest.mark.asyncio
async def test_ingest_batch_processing(api_client: AsyncClient, db_pool):
    """
    Test that batch ingestion processes all objects efficiently.

    Phase B requirement: executemany for batch insert/upsert.
    """
    # Create batch of 100 objects
    objects = [
        {
            "unique_key": f"batch-test-{i:03d}",
            "object_guid": None,
            "category": f"Category-{i % 10}",
            "name": f"Object-{i}",
            "properties": {"index": str(i)},
            "source_type": "revit"
        }
        for i in range(100)
    ]

    payload = {
        "project_code": "TEST_BATCH",
        "revision_number": 1,
        "source_type": "revit",
        "objects": objects
    }

    response = await api_client.post("/api/v1/ingest", json=payload)
    assert response.status_code == 200, f"Batch ingest failed: {response.text}"
    result = response.json()
    assert result["object_count"] == 100, f"Expected 100 objects, got {result.get('object_count')}"

    # Verify all objects were created
    async with db_pool.acquire() as conn:
        count = await conn.fetchval(
            """
            SELECT COUNT(*) FROM unified_objects uo
            JOIN revisions r ON r.id = uo.revision_id
            JOIN projects p ON p.id = r.project_id
            WHERE p.code = $1 AND r.revision_number = $2
            """,
            "TEST_BATCH",
            1
        )
        assert count == 100, f"Expected 100 objects in DB, got {count}"
