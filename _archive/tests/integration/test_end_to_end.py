"""
End-to-end integration tests for Revit → API → DB → Navisworks workflow
Phase G.1 TDD: Red → Green

Tests verify that the complete BIM data integration pipeline works correctly:
1. Revit data ingestion (Phase A legacy + Phase B unified)
2. Database storage and dual-identity pattern
3. Navisworks project detection (Phase B)
"""
import pytest
import pytest_asyncio
import uuid
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
@pytest.mark.slow
async def test_revit_to_navisworks_roundtrip(api_client: AsyncClient, db_pool):
    """
    Test complete end-to-end workflow from Revit ingestion to Navisworks detection.

    Workflow:
    1. Simulate Revit plugin extracting and sending data (Phase B format)
    2. Verify data is stored in unified_objects table with dual-identity
    3. Simulate Navisworks plugin querying for project detection
    4. Verify detection returns correct project with high confidence

    Phase G.1 TDD: Validates entire BIM data integration pipeline.
    """
    # Step 1: Ingest Revit data (simulating DXrevit plugin)
    project_code = "E2E_ROUNDTRIP_TEST"
    revision_number = 1
    test_objects = []

    # Create test objects with dual identifiers (unique_key + object_guid)
    for i in range(20):
        test_objects.append({
            "unique_key": f"e2e-revit-wall-{i:03d}",
            "object_guid": f"abcdef12-3456-7890-abcd-{i:012d}",
            "category": "Walls",
            "name": f"Basic Wall {i}",
            "properties": {
                "Height": str(3000 + i * 100),
                "Width": "200",
                "Material": "Concrete",
                "Level": "Level 1"
            },
            "source_type": "revit"
        })

    ingest_payload = {
        "project_code": project_code,
        "revision_number": revision_number,
        "source_type": "revit",
        "objects": test_objects
    }

    # Act 1: Ingest Revit data
    ingest_response = await api_client.post("/api/v1/ingest", json=ingest_payload)

    # Assert 1: Verify ingestion succeeded
    assert ingest_response.status_code == 200, \
        f"Ingest failed: {ingest_response.status_code} - {ingest_response.text}"

    ingest_result = ingest_response.json()
    assert ingest_result["success"] is True, "Ingest should succeed"
    assert ingest_result["object_count"] == 20, \
        f"Expected 20 objects ingested, got {ingest_result['object_count']}"

    revision_id = ingest_result["revision_id"]

    # Step 2: Verify data in database (dual-identity pattern)
    async with db_pool.acquire() as conn:
        # Check unified_objects table
        db_objects = await conn.fetch("""
            SELECT unique_key, object_guid, category, display_name, source_type
            FROM unified_objects
            WHERE unique_key LIKE 'e2e-revit-wall-%'
            ORDER BY unique_key
        """)

        assert len(db_objects) == 20, \
            f"Expected 20 objects in database, found {len(db_objects)}"

        # Verify dual-identity fields
        for obj in db_objects:
            assert obj["unique_key"] is not None, "unique_key should not be null"
            assert obj["object_guid"] is not None, "object_guid should not be null"
            assert obj["source_type"] == "revit", f"Expected source_type='revit', got '{obj['source_type']}'"
            assert obj["category"] == "Walls", f"Expected category='Walls', got '{obj['category']}'"

    # Step 3: Simulate Navisworks project detection
    # Extract object IDs for detection query (mix of unique_key and object_guid)
    detection_ids = []
    for i in range(10):
        # Use unique_key for first 5 objects
        if i < 5:
            detection_ids.append(f"e2e-revit-wall-{i:03d}")
        # Use object_guid for next 5 objects
        else:
            detection_ids.append(f"abcdef12-3456-7890-abcd-{i:012d}")

    detection_payload = {
        "object_ids": detection_ids,
        "min_confidence": 0.3,  # Low threshold for test
        "max_candidates": 10
    }

    # Act 2: Query project detection (simulating DXnavis plugin)
    detection_response = await api_client.post(
        "/api/v1/projects/detect-by-objects",
        json=detection_payload
    )

    # Assert 2: Verify detection succeeded
    assert detection_response.status_code == 200, \
        f"Detection failed: {detection_response.status_code} - {detection_response.text}"

    detection_result = detection_response.json()
    assert detection_result["success"] is True, "Detection should succeed"
    assert len(detection_result["detected_projects"]) > 0, \
        "Detection should find at least one project"

    # Assert 3: Verify detected project matches ingested data
    detected_project = detection_result["detected_projects"][0]
    assert detected_project["code"] == project_code, \
        f"Expected project code '{project_code}', got '{detected_project['code']}'"

    assert detected_project["match_count"] == 10, \
        f"Expected 10 matched objects, got {detected_project['match_count']}"

    # Confidence = match_count / total_objects_in_revision
    expected_confidence = 10 / 20  # 10 matched out of 20 total = 0.5
    assert abs(detected_project["confidence"] - expected_confidence) < 0.01, \
        f"Expected confidence ~{expected_confidence}, got {detected_project['confidence']}"

    # Coverage = match_count / query_object_count
    expected_coverage = 10 / 10  # All 10 query objects matched = 1.0
    assert abs(detected_project["coverage"] - expected_coverage) < 0.01, \
        f"Expected coverage ~{expected_coverage}, got {detected_project['coverage']}"

    # Assert 4: Verify dual-identity matching worked (both unique_key and object_guid)
    # The fact that we got 10 matches from mixed IDs proves both identifier types work
    print(f"\n  End-to-end workflow verified:")
    print(f"    Project: {detected_project['code']} - {detected_project['name']}")
    print(f"    Ingested: 20 objects")
    print(f"    Detected: {detected_project['match_count']} matches")
    print(f"    Confidence: {detected_project['confidence']:.2%}")
    print(f"    Coverage: {detected_project['coverage']:.2%}")
    print(f"    Dual-identity: ✓ (matched via unique_key + object_guid)")

    # Cleanup: Remove test data
    async with db_pool.acquire() as conn:
        await conn.execute(
            "DELETE FROM unified_objects WHERE unique_key LIKE 'e2e-revit-wall-%'"
        )
