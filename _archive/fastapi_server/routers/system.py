from fastapi import APIRouter, Response
from typing import Any, Dict
from datetime import datetime
import time
import logging
from prometheus_client import CONTENT_TYPE_LATEST, CollectorRegistry, Counter, Histogram, generate_latest

from ..database import db


router = APIRouter(tags=["system"])
logger = logging.getLogger(__name__)


# Prometheus metrics
REGISTRY = CollectorRegistry()
REQUEST_COUNT = Counter(
    "http_requests_total", "Total HTTP requests", ["method", "path", "status"], registry=REGISTRY
)
REQUEST_LATENCY = Histogram(
    "http_request_duration_seconds", "HTTP request latency", ["method", "path"], registry=REGISTRY
)


@router.get("/health")
async def health() -> Dict[str, Any]:
    """
    헬스체크 엔드포인트

    - API 서버 상태
    - 데이터베이스 연결 상태
    - 응답 시간 측정
    """
    # DB connectivity check with timing
    db_status = "healthy"
    db_details = {}

    try:
        start = time.perf_counter()
        await db.fetch("SELECT 1")
        db_response_time = (time.perf_counter() - start) * 1000  # 밀리초

        # 추가 DB 상태 확인
        pool_status = await db.fetch("SELECT COUNT(*) as total FROM metadata")
        metadata_count = pool_status[0]["total"] if pool_status else 0

        db_details = {
            "status": "healthy",
            "response_time_ms": round(db_response_time, 2),
            "metadata_count": metadata_count,
            **db.connection_status(),
        }
    except Exception as e:
        db_status = "unhealthy"
        db_details = {
            "status": "unhealthy",
            "error": str(e),
            **db.connection_status(),
        }
        logger.error(f"Health check DB error: {e}")

    overall_status = "healthy" if db_status == "healthy" else "degraded"

    return {
        "status": overall_status,
        "version": "1.1.0",  # Phase B: AWP 2025 v1.1.0 - Dual-identity schema
        "timestamp": datetime.utcnow().isoformat() + "Z",
        "services": {
            "database": db_details,
            "api": {
                "status": "healthy",
                "message": "API is running"
            }
        }
    }


@router.get("/metrics")
async def metrics():
    data = generate_latest(REGISTRY)
    return Response(content=data, media_type=CONTENT_TYPE_LATEST)


# Simple middleware helper for metrics (to be wired in main)
async def metrics_middleware(request, call_next):
    method = request.method
    path = request.url.path
    start = time.perf_counter()
    response = await call_next(request)
    duration = time.perf_counter() - start
    try:
        REQUEST_LATENCY.labels(method=method, path=path).observe(duration)
        REQUEST_COUNT.labels(method=method, path=path, status=str(response.status_code)).inc()
    except Exception:
        # metrics recording must never break requests
        pass
    return response
