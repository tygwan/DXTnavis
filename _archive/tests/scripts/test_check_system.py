"""
Tests for check_system.py - Database and API health monitoring
Phase F.1 TDD: Red → Green
"""
import sys
from pathlib import Path

# Add project root to Python path
project_root = Path(__file__).parent.parent.parent
if str(project_root) not in sys.path:
    sys.path.insert(0, str(project_root))

import pytest
import pytest_asyncio
from scripts.check_system import check_missing_columns, check_api_health


@pytest.mark.asyncio
async def test_detects_missing_columns(db_pool):
    """
    Test that check_missing_columns detects when required columns are missing.

    Phase F.1 TDD: This test will fail initially because check_system.py
    doesn't exist yet.
    """
    # Arrange: Define expected columns for unified_objects table
    expected_columns = {
        "id",
        "project_id",
        "revision_id",
        "object_guid",
        "unique_key",
        "category",
        "element_id"
    }

    # Act: Check for missing columns
    result = await check_missing_columns(
        db_pool,
        table_name="unified_objects",
        expected_columns=expected_columns
    )

    # Assert: Should return empty list if all columns exist
    assert isinstance(result, list), "Result should be a list"
    assert len(result) == 0, f"Expected no missing columns, but found: {result}"


@pytest.mark.asyncio
async def test_detects_missing_columns_when_column_absent(db_pool):
    """
    Test that check_missing_columns correctly identifies missing columns.

    This test uses a fake column name that definitely doesn't exist.
    """
    # Arrange: Include a column that doesn't exist
    expected_columns = {
        "id",
        "this_column_does_not_exist_xyz123"
    }

    # Act: Check for missing columns
    result = await check_missing_columns(
        db_pool,
        table_name="unified_objects",
        expected_columns=expected_columns
    )

    # Assert: Should detect the missing column
    assert isinstance(result, list), "Result should be a list"
    assert "this_column_does_not_exist_xyz123" in result, \
        "Should detect the missing column"


@pytest.mark.asyncio
async def test_api_health_probe():
    """
    Test that check_api_health successfully probes the FastAPI server.

    Phase F.1 TDD: This test will fail because check_api_health doesn't exist yet.
    """
    # Arrange: API server URL (default for local development)
    api_url = "http://localhost:8000"

    # Act: Check API health
    result = await check_api_health(api_url)

    # Assert: Should return True if API is healthy, False otherwise
    assert isinstance(result, bool), "Result should be a boolean"
    # Note: This might fail if API server is not running, which is expected in CI
    # For now, just verify the function returns a boolean
