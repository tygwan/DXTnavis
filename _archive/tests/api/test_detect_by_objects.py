"""
Test project detection API with latest revision constraint (Phase B)

Tests verify that /api/v1/projects/detect-by-objects:
1. Only considers objects from the latest revision for each project
2. Correctly calculates confidence and coverage
3. Implements response caching with TTL
4. Handles edge cases (empty input, no matches, etc.)
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


@pytest_asyncio.fixture
async def setup_test_data(db_pool):
    """
    Setup test data for detection tests.

    Creates:
    - Project A with revision 1 (10 objects) and revision 2 (5 objects)
    - Project B with revision 1 (8 objects)
    """
    # Cleanup first (in case of previous test failures)
    async with db_pool.acquire() as conn:
        await conn.execute(
            "DELETE FROM projects WHERE code IN ('DETECT_TEST_A', 'DETECT_TEST_B')"
        )

    async with db_pool.acquire() as conn:
        # Create Project A
        project_a_id = await conn.fetchval(
            "INSERT INTO projects (id, code, name, created_by) "
            "VALUES (gen_random_uuid(), $1, $2, 'test') RETURNING id",
            "DETECT_TEST_A",
            "Detection Test Project A"
        )

        # Project A - Revision 1 (older)
        rev_a1_id = await conn.fetchval(
            "INSERT INTO revisions (id, project_id, revision_number, source_type, created_by) "
            "VALUES (gen_random_uuid(), $1, 1, 'revit', 'test') RETURNING id",
            project_a_id
        )

        # Insert 10 objects in revision 1
        for i in range(10):
            await conn.execute(
                """
                INSERT INTO unified_objects
                (project_id, revision_id, unique_key, object_guid, category, display_name, source_type, properties)
                VALUES
                ($1, $2, $3, $4, 'Walls', $5, 'revit', '{}'::jsonb)
                """,
                project_a_id,
                rev_a1_id,
                f"proj-a-rev1-obj-{i}",
                f"aaaabbbb-cccc-dddd-eeee-00000000000{i}",
                f"Wall-A-R1-{i}"
            )

        # Project A - Revision 2 (latest)
        rev_a2_id = await conn.fetchval(
            "INSERT INTO revisions (id, project_id, revision_number, source_type, created_by) "
            "VALUES (gen_random_uuid(), $1, 2, 'revit', 'test') RETURNING id",
            project_a_id
        )

        # Insert 5 objects in revision 2
        for i in range(5):
            await conn.execute(
                """
                INSERT INTO unified_objects
                (project_id, revision_id, unique_key, object_guid, category, display_name, source_type, properties)
                VALUES
                ($1, $2, $3, $4, 'Walls', $5, 'revit', '{}'::jsonb)
                """,
                project_a_id,
                rev_a2_id,
                f"proj-a-rev2-obj-{i}",
                f"bbbbcccc-dddd-eeee-ffff-00000000000{i}",
                f"Wall-A-R2-{i}"
            )

        # Create Project B
        project_b_id = await conn.fetchval(
            "INSERT INTO projects (id, code, name, created_by) "
            "VALUES (gen_random_uuid(), $1, $2, 'test') RETURNING id",
            "DETECT_TEST_B",
            "Detection Test Project B"
        )

        # Project B - Revision 1 (only revision)
        rev_b1_id = await conn.fetchval(
            "INSERT INTO revisions (id, project_id, revision_number, source_type, created_by) "
            "VALUES (gen_random_uuid(), $1, 1, 'revit', 'test') RETURNING id",
            project_b_id
        )

        # Insert 8 objects in Project B
        for i in range(8):
            await conn.execute(
                """
                INSERT INTO unified_objects
                (project_id, revision_id, unique_key, object_guid, category, display_name, source_type, properties)
                VALUES
                ($1, $2, $3, $4, 'Columns', $5, 'revit', '{}'::jsonb)
                """,
                project_b_id,
                rev_b1_id,
                f"proj-b-obj-{i}",
                f"ccccdddd-eeee-ffff-0000-00000000000{i}",
                f"Column-B-{i}"
            )

    yield

    # Cleanup - CASCADE will delete revisions and unified_objects
    async with db_pool.acquire() as conn:
        await conn.execute(
            "DELETE FROM projects WHERE code IN ('DETECT_TEST_A', 'DETECT_TEST_B')"
        )


@pytest.mark.asyncio
async def test_only_latest_revision_considered(api_client: AsyncClient, setup_test_data):
    """
    Test that detection only considers objects from the latest revision.

    Project A has:
    - Revision 1: 10 objects (proj-a-rev1-obj-0 to 9)
    - Revision 2: 5 objects (proj-a-rev2-obj-0 to 4)

    When detecting with rev2 object IDs, confidence should be based on 5 objects, not 15.
    """
    # Query with 3 objects from revision 2 (latest)
    detect_request = {
        "object_ids": [
            "proj-a-rev2-obj-0",
            "proj-a-rev2-obj-1",
            "proj-a-rev2-obj-2"
        ],
        "min_confidence": 0.5,
        "max_candidates": 3
    }

    response = await api_client.post("/api/v1/projects/detect-by-objects", json=detect_request)
    assert response.status_code == 200, f"Detection failed: {response.text}"

    result = response.json()
    assert result["success"] is True
    assert len(result["detected_projects"]) > 0, "No projects detected"

    # Find Project A in results
    project_a = next(
        (p for p in result["detected_projects"] if p["code"] == "DETECT_TEST_A"),
        None
    )
    assert project_a is not None, "Project A not detected"

    # Confidence = matched / total_in_latest_revision = 3 / 5 = 0.6
    expected_confidence = 3 / 5
    assert abs(project_a["confidence"] - expected_confidence) < 0.01, \
        f"Confidence should be {expected_confidence}, got {project_a['confidence']}"

    assert project_a["match_count"] == 3, "Should match 3 objects"
    assert project_a["total_objects"] == 5, "Total should be 5 (latest revision only)"


@pytest.mark.asyncio
async def test_detection_cache_short_circuit(api_client: AsyncClient, setup_test_data):
    """
    Test that response caching works correctly.

    Phase B requirement: Cache detection results with TTL=300s.
    Same query should return cached result (faster response).
    """
    detect_request = {
        "object_ids": ["proj-a-rev2-obj-0", "proj-a-rev2-obj-1"],
        "min_confidence": 0.3,
        "max_candidates": 5
    }

    # First request (cache miss)
    response1 = await api_client.post("/api/v1/projects/detect-by-objects", json=detect_request)
    assert response1.status_code == 200

    # Second request (cache hit)
    response2 = await api_client.post("/api/v1/projects/detect-by-objects", json=detect_request)
    assert response2.status_code == 200

    # Results should be identical
    result1 = response1.json()
    result2 = response2.json()
    assert result1 == result2, "Cached response should match original"


@pytest.mark.asyncio
async def test_detection_with_guid_fallback(api_client: AsyncClient, setup_test_data):
    """
    Test that detection works with object_guid fallback.

    Phase B requirement: Match by unique_key OR object_guid.
    """
    # Query using object_guid instead of unique_key
    detect_request = {
        "object_ids": [
            "bbbbcccc-dddd-eeee-ffff-000000000000",  # GUID from Project A Rev 2
            "bbbbcccc-dddd-eeee-ffff-000000000001",
            "bbbbcccc-dddd-eeee-ffff-000000000002"
        ],
        "min_confidence": 0.5,
        "max_candidates": 3
    }

    response = await api_client.post("/api/v1/projects/detect-by-objects", json=detect_request)
    assert response.status_code == 200

    result = response.json()
    project_a = next(
        (p for p in result["detected_projects"] if p["code"] == "DETECT_TEST_A"),
        None
    )
    assert project_a is not None, "Detection by GUID should work"
    assert project_a["match_count"] == 3, "Should match 3 objects by GUID"


@pytest.mark.asyncio
async def test_detection_respects_min_confidence(api_client: AsyncClient, setup_test_data):
    """
    Test that min_confidence filter works correctly.
    """
    # Only 1 object matches out of 5 total = 0.2 confidence
    detect_request = {
        "object_ids": ["proj-a-rev2-obj-0"],
        "min_confidence": 0.5,  # Higher than 0.2
        "max_candidates": 3
    }

    response = await api_client.post("/api/v1/projects/detect-by-objects", json=detect_request)
    assert response.status_code == 200

    result = response.json()
    # Project A should NOT be in results (confidence too low)
    project_a = next(
        (p for p in result["detected_projects"] if p["code"] == "DETECT_TEST_A"),
        None
    )
    assert project_a is None, "Project with low confidence should be filtered out"


@pytest.mark.asyncio
async def test_detection_empty_input(api_client: AsyncClient):
    """
    Test that empty object_ids list returns error.
    """
    detect_request = {
        "object_ids": [],
        "min_confidence": 0.7,
        "max_candidates": 3
    }

    response = await api_client.post("/api/v1/projects/detect-by-objects", json=detect_request)
    # Should return 422 (validation error) due to min_items=1 constraint
    assert response.status_code == 422, "Empty object_ids should be rejected"


@pytest.mark.asyncio
async def test_detection_coverage_metric(api_client: AsyncClient, setup_test_data):
    """
    Test that coverage metric is calculated correctly.

    Coverage = match_count / input_count
    """
    # Send 4 object IDs, 3 match Project A
    detect_request = {
        "object_ids": [
            "proj-a-rev2-obj-0",
            "proj-a-rev2-obj-1",
            "proj-a-rev2-obj-2",
            "non-existent-obj-999"  # Doesn't exist
        ],
        "min_confidence": 0.5,
        "max_candidates": 3
    }

    response = await api_client.post("/api/v1/projects/detect-by-objects", json=detect_request)
    assert response.status_code == 200

    result = response.json()
    project_a = next(
        (p for p in result["detected_projects"] if p["code"] == "DETECT_TEST_A"),
        None
    )
    assert project_a is not None

    # Coverage = 3 matched / 4 input = 0.75
    expected_coverage = 3 / 4
    assert abs(project_a["coverage"] - expected_coverage) < 0.01, \
        f"Coverage should be {expected_coverage}, got {project_a['coverage']}"
