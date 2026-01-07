"""
System health check utilities for AWP 2025 BIM Data Integration System
Phase F.1 TDD: Red → Green
"""
import asyncpg
import httpx
from typing import List, Set


async def check_missing_columns(
    db_pool: asyncpg.Pool,
    table_name: str,
    expected_columns: Set[str]
) -> List[str]:
    """
    Check if a database table has all expected columns.

    Args:
        db_pool: Database connection pool
        table_name: Name of the table to check
        expected_columns: Set of expected column names

    Returns:
        List of missing column names (empty if all exist)

    Phase F.1 TDD: Minimal implementation to pass tests
    """
    async with db_pool.acquire() as conn:
        # Query information_schema to get actual columns
        query = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = $1
            AND table_schema = 'public'
        """
        rows = await conn.fetch(query, table_name)

        # Extract actual column names
        actual_columns = {row['column_name'] for row in rows}

        # Find missing columns
        missing_columns = expected_columns - actual_columns

        return sorted(list(missing_columns))


async def check_api_health(api_url: str, timeout: float = 5.0) -> bool:
    """
    Check if the FastAPI server is responding.

    Args:
        api_url: Base URL of the API server (e.g., http://localhost:8000)
        timeout: Request timeout in seconds (default: 5.0)

    Returns:
        True if API is healthy, False otherwise

    Phase F.1 TDD: Minimal implementation to pass tests
    """
    try:
        async with httpx.AsyncClient(timeout=timeout) as client:
            # Try to access the /api/v1/system/health endpoint
            response = await client.get(f"{api_url}/api/v1/system/health")
            return response.status_code == 200
    except (httpx.RequestError, httpx.HTTPStatusError, Exception):
        # Any error means API is not healthy
        return False
