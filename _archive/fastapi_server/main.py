import logging
from pathlib import Path
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.middleware.trustedhost import TrustedHostMiddleware
from fastapi.exceptions import RequestValidationError
from fastapi import HTTPException
import uvicorn
from .config import settings
from .routers import ingest, analytics, timeliner, system, dashboard, projects, revisions, navisworks
from contextlib import asynccontextmanager
from .database import db
from .middleware.security import SecurityHeadersMiddleware
from .routers.system import metrics_middleware
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from .middleware.error_handler import (
    validation_exception_handler,
    http_exception_handler,
    general_exception_handler,
    database_exception_handler,
    DatabaseError
)


logging.basicConfig(
    level=getattr(logging, settings.LOG_LEVEL.upper(), logging.INFO),
    format='[%(asctime)s] [%(levelname)s] [%(name)s] %(message)s'
)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    try:
        await db.connect_pool()
    except DatabaseError as exc:
        logger.error("Database pool initialization failed: %s", exc)
    try:
        yield
    finally:
        await db.close_pool()


app = FastAPI(
    title="DX Platform API",
    version="1.0.0",
    description="BIM 데이터 통합 플랫폼 API - Revit에서 Navisworks까지",
    lifespan=lifespan,
    docs_url="/docs",
    redoc_url="/redoc"
)

# 에러 핸들러 등록
app.add_exception_handler(RequestValidationError, validation_exception_handler)
app.add_exception_handler(HTTPException, http_exception_handler)
app.add_exception_handler(DatabaseError, database_exception_handler)
app.add_exception_handler(Exception, general_exception_handler)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.ALLOWED_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Trusted hosts
app.add_middleware(TrustedHostMiddleware, allowed_hosts=settings.ALLOWED_HOSTS)

# Security headers
app.add_middleware(SecurityHeadersMiddleware)

# Metrics middleware
app.middleware("http")(metrics_middleware)

static_dir = Path(__file__).resolve().parent / "static"
if static_dir.exists():
    app.mount("/static", StaticFiles(directory=static_dir), name="static")

app.include_router(dashboard.router)
app.include_router(projects.router)
app.include_router(revisions.router)
app.include_router(navisworks.router)
app.include_router(ingest.router)
app.include_router(analytics.router)
app.include_router(timeliner.router)
app.include_router(system.router)


@app.get("/api")
def root():
    return {"service": "DX Platform API", "status": "running"}


@app.get("/", include_in_schema=False)
def serve_index():
    index_path = static_dir / "index.html"
    if index_path.exists():
        return FileResponse(index_path)
    return {"message": "Static index not found. Visit /docs for API reference."}


if __name__ == "__main__":
    uvicorn.run("fastapi_server.main:app", host=settings.HOST, port=settings.PORT, reload=settings.DEBUG)
