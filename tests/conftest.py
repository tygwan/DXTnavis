"""
Test configuration and fixtures for AWP 2025 BIM Data Integration System
"""
import sys
from pathlib import Path

# Add project root to Python path for scripts module import
project_root = Path(__file__).parent.parent
if str(project_root) not in sys.path:
    sys.path.insert(0, str(project_root))

import pytest
import pytest_asyncio
import asyncpg
import os
from typing import AsyncGenerator


@pytest_asyncio.fixture(scope="function")
async def db_pool() -> AsyncGenerator[asyncpg.Pool, None]:
    """
    Create a connection pool to the test database.

    Environment variables required:
    - POSTGRES_HOST (default: localhost)
    - POSTGRES_PORT (default: 5432)
    - POSTGRES_DATABASE (default: DX_platform)
    - POSTGRES_USER (default: postgres)
    - POSTGRES_PASSWORD
    """
    host = os.getenv("POSTGRES_HOST", "localhost")
    port = os.getenv("POSTGRES_PORT", "5432")
    database = os.getenv("POSTGRES_DATABASE", "DX_platform")
    user = os.getenv("POSTGRES_USER", "postgres")
    password = os.getenv("POSTGRES_PASSWORD", "123456")

    dsn = f"postgresql://{user}:{password}@{host}:{port}/{database}"

    pool = await asyncpg.create_pool(dsn=dsn, min_size=1, max_size=5)

    try:
        yield pool
    finally:
        await pool.close()


@pytest_asyncio.fixture
async def db_connection(db_pool: asyncpg.Pool) -> AsyncGenerator[asyncpg.Connection, None]:
    """Get a single connection from the pool for a test."""
    async with db_pool.acquire() as conn:
        yield conn
