import asyncpg
import logging
from typing import Optional, Sequence, Tuple, Any, List
from .config import settings


logger = logging.getLogger(__name__)


class AsyncDatabase:
    def __init__(self):
        self.pool: Optional[asyncpg.Pool] = None

    async def connect_pool(self):
        if self.pool is None:
            self.pool = await asyncpg.create_pool(
                dsn=settings.DATABASE_URL,
                min_size=settings.DB_POOL_MIN,
                max_size=settings.DB_POOL_MAX,
            )
            logger.info("Async DB pool initialized")

    async def close_pool(self):
        if self.pool is not None:
            await self.pool.close()
            self.pool = None
            logger.info("Async DB pool closed")

    async def execute(self, query: str, params: Tuple[Any, ...] = ()):  # returns status text
        assert self.pool is not None, "DB pool not initialized"
        async with self.pool.acquire() as conn:
            return await conn.execute(query, *params)

    async def executemany(self, query: str, seq_of_params: Sequence[Tuple[Any, ...]]):
        assert self.pool is not None, "DB pool not initialized"
        if not seq_of_params:
            return 0
        async with self.pool.acquire() as conn:
            await conn.executemany(query, seq_of_params)
            return len(seq_of_params)

    async def fetch(self, query: str, params: Tuple[Any, ...] = ()) -> List[asyncpg.Record]:
        assert self.pool is not None, "DB pool not initialized"
        async with self.pool.acquire() as conn:
            return await conn.fetch(query, *params)

    async def fetchrow(self, query: str, params: Tuple[Any, ...] = ()) -> Optional[asyncpg.Record]:
        assert self.pool is not None, "DB pool not initialized"
        async with self.pool.acquire() as conn:
            return await conn.fetchrow(query, *params)


db = AsyncDatabase()
