import logging
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.middleware.trustedhost import TrustedHostMiddleware
from fastapi.exceptions import RequestValidationError
from fastapi import HTTPException
import uvicorn
from .config import settings
from .routers import ingest, analytics, timeliner, system
from contextlib import asynccontextmanager
from .database import db
from .middleware.security import SecurityHeadersMiddleware
from .routers.system import metrics_middleware
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
    await db.connect_pool()
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

app.include_router(ingest.router)
app.include_router(analytics.router)
app.include_router(timeliner.router)
app.include_router(system.router)


@app.get("/")
def root():
    return {"service": "DX Platform API", "status": "running"}


if __name__ == "__main__":
    uvicorn.run("fastapi_server.main:app", host=settings.HOST, port=settings.PORT, reload=settings.DEBUG)
