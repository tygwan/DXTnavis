"""
Performance tests for project detection endpoint latency
Phase G.1 TDD: Red → Green

Tests verify that the /api/v1/projects/detect-by-objects endpoint
maintains acceptable p95 latency under typical workload conditions.
"""
import pytest
import pytest_asyncio
import time
import statistics
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
async def test_project_data(api_client: AsyncClient, db_pool):
    """Create test project with sample objects for detection testing."""
    # Create test project and objects
    project_code = "LATENCY_TEST"
    revision_number = 1
    num_objects = 50  # Create 50 test objects for detection

    objects = []
    for i in range(num_objects):
        objects.append({
            "unique_key": f"latency-test-obj-{i:03d}",
            "object_guid": f"12345678-1234-1234-1234-{i:012d}",
            "category": "TestCategory",
            "name": f"Detection Test Object {i}",
            "properties": {"test": f"value-{i}"},
            "source_type": "revit"
        })

    # Ingest test data
    payload = {
        "project_code": project_code,
        "revision_number": revision_number,
        "source_type": "revit",
        "objects": objects
    }

    response = await api_client.post("/api/v1/ingest", json=payload)
    assert response.status_code == 200, f"Failed to create test data: {response.text}"

    # Return object IDs for detection queries
    object_ids = [obj["unique_key"] for obj in objects]

    yield {
        "project_code": project_code,
        "object_ids": object_ids
    }

    # Cleanup
    async with db_pool.acquire() as conn:
        await conn.execute(
            "DELETE FROM unified_objects WHERE unique_key LIKE 'latency-test-obj-%'"
        )


@pytest.mark.asyncio
@pytest.mark.slow
async def test_detection_p95_under_threshold(
    api_client: AsyncClient,
    test_project_data: dict
):
    """
    Test that project detection p95 latency is under acceptable threshold.

    Performance requirement: p95 latency should be < 200ms
    This ensures responsive user experience for project detection workflows.

    Phase G.1 TDD: This test will initially fail if detection is not optimized.
    """
    # Arrange: Prepare detection queries with varying object counts
    object_ids = test_project_data["object_ids"]
    num_requests = 30  # Run 30 requests to get meaningful p95 statistics
    latencies = []

    # Test with varying query sizes (5, 10, 20 objects)
    query_sizes = [5, 10, 20]
    requests_per_size = num_requests // len(query_sizes)

    # Act: Execute detection requests and measure latency
    for query_size in query_sizes:
        for _ in range(requests_per_size):
            # Select random subset of objects
            import random
            sample_ids = random.sample(object_ids, min(query_size, len(object_ids)))

            payload = {
                "object_ids": sample_ids,
                "min_confidence": 0.5,
                "max_candidates": 10
            }

            start_time = time.perf_counter()
            response = await api_client.post("/api/v1/projects/detect-by-objects", json=payload)
            end_time = time.perf_counter()

            latency_ms = (end_time - start_time) * 1000
            latencies.append(latency_ms)

            # Verify successful response
            assert response.status_code == 200, f"Expected 200, got {response.status_code}: {response.text}"

    # Assert: Verify p95 latency is under threshold
    latencies.sort()
    p95_index = int(len(latencies) * 0.95)
    p95_latency = latencies[p95_index]

    # Performance threshold: p95 < 200ms
    max_p95_latency = 200.0
    assert p95_latency < max_p95_latency, \
        f"p95 latency {p95_latency:.2f}ms exceeds threshold of {max_p95_latency}ms"

    # Calculate and log detailed metrics
    avg_latency = statistics.mean(latencies)
    p50_latency = latencies[int(len(latencies) * 0.50)]
    p99_latency = latencies[int(len(latencies) * 0.99)]
    min_latency = min(latencies)
    max_latency = max(latencies)

    print(f"\n  Detection latency metrics ({num_requests} requests):")
    print(f"    Min:  {min_latency:.2f}ms")
    print(f"    p50:  {p50_latency:.2f}ms")
    print(f"    Avg:  {avg_latency:.2f}ms")
    print(f"    p95:  {p95_latency:.2f}ms")
    print(f"    p99:  {p99_latency:.2f}ms")
    print(f"    Max:  {max_latency:.2f}ms")
    print(f"    Query sizes tested: {query_sizes}")
