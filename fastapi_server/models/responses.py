"""
표준화된 API 응답 모델
"""
from pydantic import BaseModel, Field
from typing import Optional, Any, List, Dict
from datetime import datetime
from enum import Enum


class ResponseStatus(str, Enum):
    """응답 상태"""
    SUCCESS = "success"
    ERROR = "error"
    PARTIAL = "partial"


class ErrorDetail(BaseModel):
    """에러 상세 정보"""
    code: str = Field(..., description="에러 코드")
    message: str = Field(..., description="에러 메시지")
    field: Optional[str] = Field(None, description="에러 발생 필드")
    details: Optional[Dict[str, Any]] = Field(None, description="추가 상세 정보")


class StandardResponse(BaseModel):
    """표준 응답 모델"""
    status: ResponseStatus = Field(..., description="응답 상태")
    message: str = Field(..., description="응답 메시지")
    data: Optional[Any] = Field(None, description="응답 데이터")
    errors: Optional[List[ErrorDetail]] = Field(None, description="에러 목록")
    timestamp: datetime = Field(default_factory=datetime.utcnow, description="응답 시각 (UTC)")
    request_id: Optional[str] = Field(None, description="요청 추적 ID")

    class Config:
        json_schema_extra = {
            "example": {
                "status": "success",
                "message": "Operation completed successfully",
                "data": {"key": "value"},
                "errors": None,
                "timestamp": "2025-10-14T12:00:00Z",
                "request_id": "req-123456"
            }
        }


class PaginatedResponse(BaseModel):
    """페이지네이션 응답 모델"""
    status: ResponseStatus = Field(..., description="응답 상태")
    message: str = Field(..., description="응답 메시지")
    data: List[Any] = Field(..., description="응답 데이터 목록")
    pagination: Dict[str, int] = Field(..., description="페이지 정보")
    timestamp: datetime = Field(default_factory=datetime.utcnow, description="응답 시각 (UTC)")
    request_id: Optional[str] = Field(None, description="요청 추적 ID")

    class Config:
        json_schema_extra = {
            "example": {
                "status": "success",
                "message": "Data retrieved successfully",
                "data": [{"id": 1}, {"id": 2}],
                "pagination": {
                    "total": 100,
                    "page": 1,
                    "page_size": 10,
                    "total_pages": 10
                },
                "timestamp": "2025-10-14T12:00:00Z",
                "request_id": "req-123456"
            }
        }


class IngestResponse(BaseModel):
    """데이터 수집 응답"""
    status: ResponseStatus = Field(..., description="수집 상태")
    message: str = Field(..., description="수집 결과 메시지")
    model_version: str = Field(..., description="모델 버전")
    objects_inserted: int = Field(..., description="삽입된 객체 수")
    relationships_inserted: int = Field(..., description="삽입된 관계 수")
    processing_time_ms: float = Field(..., description="처리 시간 (밀리초)")
    timestamp: datetime = Field(default_factory=datetime.utcnow, description="처리 완료 시각")
    warnings: Optional[List[str]] = Field(None, description="경고 메시지 목록")

    class Config:
        json_schema_extra = {
            "example": {
                "status": "success",
                "message": "Data ingested successfully",
                "model_version": "v1.0.0",
                "objects_inserted": 1500,
                "relationships_inserted": 300,
                "processing_time_ms": 1250.5,
                "timestamp": "2025-10-14T12:00:00Z",
                "warnings": None
            }
        }


class HealthCheckResponse(BaseModel):
    """헬스체크 응답"""
    status: str = Field(..., description="서비스 상태")
    version: str = Field(..., description="API 버전")
    timestamp: datetime = Field(default_factory=datetime.utcnow, description="체크 시각")
    services: Dict[str, Any] = Field(..., description="서비스 상태 상세")

    class Config:
        json_schema_extra = {
            "example": {
                "status": "healthy",
                "version": "1.0.0",
                "timestamp": "2025-10-14T12:00:00Z",
                "services": {
                    "database": {
                        "status": "healthy",
                        "response_time_ms": 5.2,
                        "pool_size": 10,
                        "pool_available": 8
                    },
                    "api": {
                        "status": "healthy",
                        "uptime_seconds": 3600
                    }
                }
            }
        }


class MetricsResponse(BaseModel):
    """메트릭 응답"""
    total_requests: int = Field(..., description="총 요청 수")
    requests_by_endpoint: Dict[str, int] = Field(..., description="엔드포인트별 요청 수")
    average_response_time_ms: float = Field(..., description="평균 응답 시간 (밀리초)")
    error_count: int = Field(..., description="에러 발생 횟수")
    uptime_seconds: float = Field(..., description="가동 시간 (초)")
    timestamp: datetime = Field(default_factory=datetime.utcnow, description="측정 시각")
