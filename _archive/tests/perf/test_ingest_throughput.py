"""
Performance tests for ingest endpoint throughput
Phase G.1 TDD: Red → Green

Tests verify that the /api/v1/ingest endpoint can handle batch processing
within acceptable performance thresholds.
"""
import pytest
import pytest_asyncio
import time
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
async def test_ingest_batch_processing_time(api_client: AsyncClient, db_pool):
    """
    Test that batch ingest of 100 objects completes within performance threshold.

    Performance requirement: 100 objects should be processed in < 5 seconds
    This ensures acceptable throughput for production workloads.

    Phase G.1 TDD: This test will initially fail if performance is not optimized.
    """
    # Arrange: Prepare batch of 100 objects
    batch_size = 100
    objects = []

    for i in range(batch_size):
        objects.append({
            "unique_key": f"perf-test-object-{i:05d}",
            "object_guid": f"a1b2c3d4-e5f6-7890-abcd-{i:012d}",
            "category": "TestCategory",
            "name": f"Test Object {i}",
            "properties": {
                "Height": str(3000 + i),
                "Width": str(200 + i),
                "Material": f"Material-{i % 10}"
            },
            "source_type": "revit"
        })

    payload = {
        "project_code": "PERF_TEST",
        "revision_number": 1,
        "source_type": "revit",
        "objects": objects
    }

    # Act: Measure ingest processing time
    start_time = time.perf_counter()
    response = await api_client.post("/api/v1/ingest", json=payload)
    end_time = time.perf_counter()

    processing_time = end_time - start_time

    # Assert: Verify response and performance
    assert response.status_code == 200, f"Expected 200, got {response.status_code}: {response.text}"

    result = response.json()
    assert result["success"] is True, "Ingest should succeed"
    assert result["object_count"] == batch_size, \
        f"Expected {batch_size} objects processed, got {result['object_count']}"

    # Performance threshold: 100 objects in < 5 seconds
    max_processing_time = 5.0
    assert processing_time < max_processing_time, \
        f"Batch processing took {processing_time:.2f}s, exceeds threshold of {max_processing_time}s"

    # Calculate and log throughput
    throughput = batch_size / processing_time
    print(f"\n  Performance metrics:")
    print(f"    Objects processed: {batch_size}")
    print(f"    Processing time: {processing_time:.3f}s")
    print(f"    Throughput: {throughput:.1f} objects/sec")

    # Clean up test data
    async with db_pool.acquire() as conn:
        await conn.execute(
            "DELETE FROM unified_objects WHERE unique_key LIKE 'perf-test-object-%'"
        )
