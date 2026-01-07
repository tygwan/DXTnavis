import asyncio
import logging
from typing import Any, Dict, List, Optional, Sequence, Tuple

import asyncpg

from .config import settings
from .middleware.error_handler import DatabaseError


logger = logging.getLogger(__name__)


class AsyncDatabase:
    """AsyncPG helper that keeps connection pool state and exposes safe helpers."""

    def __init__(self) -> None:
        self.pool: Optional[asyncpg.Pool] = None
        self._last_error: Optional[BaseException] = None

    @property
    def last_error(self) -> Optional[BaseException]:
        return self._last_error

    def _mark_failure(self, exc: BaseException) -> None:
        self._last_error = exc
        self.pool = None

    async def connect_pool(self, retries: int = 3, initial_delay: float = 0.5) -> None:
        """Initialize the asyncpg pool with retries so startup survives transient errors."""
        if self.pool is not None:
            return

        delay = initial_delay
        attempt = 1
        last_exc: Optional[BaseException] = None

        while attempt <= max(retries, 1):
            try:
                self.pool = await asyncpg.create_pool(
                    dsn=settings.DATABASE_URL,
                    min_size=settings.DB_POOL_MIN,
                    max_size=settings.DB_POOL_MAX,
                )
                self._last_error = None
                logger.info("Async DB pool initialized (attempt %d)", attempt)
                return
            except (asyncpg.PostgresError, OSError) as exc:
                last_exc = exc
                logger.warning(
                    "Database connection attempt %d failed: %s",
                    attempt,
                    exc,
                )
                if attempt == retries:
                    break
                await asyncio.sleep(delay)
                delay *= 2
                attempt += 1

        assert last_exc is not None  # for type checkers
        self._mark_failure(last_exc)
        raise DatabaseError(
            "Failed to initialize database connection pool.",
            code="DB_POOL_INIT_FAILED",
        ) from last_exc

    async def close_pool(self) -> None:
        if self.pool is not None:
            await self.pool.close()
            self.pool = None
            logger.info("Async DB pool closed")

    def _ensure_pool(self) -> asyncpg.Pool:
        if self.pool is not None:
            return self.pool

        message = "Database connection is not available."
        if self._last_error:
            message = f"{message} Last error: {self._last_error!s}"
        raise DatabaseError(message, code="DB_POOL_UNAVAILABLE")

    @staticmethod
    def _normalize_params(params: Any) -> Tuple[Any, ...]:
        if params is None:
            return ()
        if isinstance(params, tuple):
            return params
        if isinstance(params, list):
            return tuple(params)
        return (params,)

    async def execute(self, query: str, params: Any = ()) -> str:
        pool = self._ensure_pool()
        async with pool.acquire() as conn:
            norm = self._normalize_params(params)
            return await conn.execute(query, *norm)

    async def executemany(self, query: str, seq_of_params: Sequence[Tuple[Any, ...]]) -> int:
        pool = self._ensure_pool()
        if not seq_of_params:
            return 0
        async with pool.acquire() as conn:
            await conn.executemany(query, seq_of_params)
            return len(seq_of_params)

    async def fetch(self, query: str, params: Any = ()) -> List[asyncpg.Record]:
        pool = self._ensure_pool()
        async with pool.acquire() as conn:
            norm = self._normalize_params(params)
            return await conn.fetch(query, *norm)

    async def fetchrow(self, query: str, params: Any = ()) -> Optional[asyncpg.Record]:
        pool = self._ensure_pool()
        async with pool.acquire() as conn:
            norm = self._normalize_params(params)
            return await conn.fetchrow(query, *norm)

    async def fetchval(self, query: str, params: Any = ()) -> Any:
        pool = self._ensure_pool()
        async with pool.acquire() as conn:
            norm = self._normalize_params(params)
            return await conn.fetchval(query, *norm)

    def connection_status(self) -> Dict[str, Any]:
        """Useful snapshot for health checks."""
        return {
            "connected": self.pool is not None,
            "last_error": str(self._last_error) if self._last_error else None,
        }


db = AsyncDatabase()


async def get_db() -> AsyncDatabase:
    """
    FastAPI dependency injection용 데이터베이스 인스턴스 제공

    Usage:
        @app.get("/endpoint")
        async def endpoint(db: AsyncDatabase = Depends(get_db)):
            ...
    """
    return db
