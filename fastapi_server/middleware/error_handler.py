"""
전역 에러 핸들러 미들웨어
"""
import logging
import traceback
from fastapi import Request, status
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from pydantic import ValidationError
from datetime import datetime
from typing import Union

from ..models.responses import ResponseStatus, ErrorDetail, StandardResponse


logger = logging.getLogger(__name__)


async def validation_exception_handler(request: Request, exc: Union[RequestValidationError, ValidationError]):
    """입력 검증 에러 핸들러"""
    errors = []
    for error in exc.errors():
        errors.append(ErrorDetail(
            code="VALIDATION_ERROR",
            message=error.get("msg", "Validation failed"),
            field=".".join(str(loc) for loc in error.get("loc", [])),
            details={"type": error.get("type")}
        ))

    response = StandardResponse(
        status=ResponseStatus.ERROR,
        message="입력 데이터 검증 실패",
        data=None,
        errors=errors,
        timestamp=datetime.utcnow()
    )

    logger.warning(f"Validation error on {request.url.path}: {errors}")

    return JSONResponse(
        status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
        content=response.model_dump()
    )


async def http_exception_handler(request: Request, exc: Exception):
    """HTTP 예외 핸들러"""
    status_code = getattr(exc, "status_code", status.HTTP_500_INTERNAL_SERVER_ERROR)
    detail = getattr(exc, "detail", "Internal Server Error")

    error = ErrorDetail(
        code=f"HTTP_{status_code}",
        message=detail,
        field=None,
        details={"path": str(request.url.path)}
    )

    response = StandardResponse(
        status=ResponseStatus.ERROR,
        message=detail if isinstance(detail, str) else "HTTP Error",
        data=None,
        errors=[error],
        timestamp=datetime.utcnow()
    )

    if status_code >= 500:
        logger.error(f"HTTP {status_code} on {request.url.path}: {detail}")
    else:
        logger.warning(f"HTTP {status_code} on {request.url.path}: {detail}")

    return JSONResponse(
        status_code=status_code,
        content=response.model_dump()
    )


async def general_exception_handler(request: Request, exc: Exception):
    """일반 예외 핸들러"""
    error_id = f"err-{int(datetime.utcnow().timestamp())}"

    error = ErrorDetail(
        code="INTERNAL_ERROR",
        message="서버 내부 오류가 발생했습니다",
        field=None,
        details={
            "error_id": error_id,
            "type": type(exc).__name__
        }
    )

    response = StandardResponse(
        status=ResponseStatus.ERROR,
        message="Internal Server Error",
        data=None,
        errors=[error],
        timestamp=datetime.utcnow(),
        request_id=error_id
    )

    # 상세 에러 로그 (프로덕션에서는 민감 정보 제외)
    logger.error(
        f"Unhandled exception [{error_id}] on {request.url.path}: {str(exc)}\n"
        f"Traceback: {traceback.format_exc()}"
    )

    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content=response.model_dump()
    )


class DatabaseError(Exception):
    """데이터베이스 에러"""
    def __init__(self, message: str, code: str = "DB_ERROR"):
        self.message = message
        self.code = code
        super().__init__(self.message)


async def database_exception_handler(request: Request, exc: DatabaseError):
    """데이터베이스 예외 핸들러"""
    error = ErrorDetail(
        code=exc.code,
        message=exc.message,
        field=None,
        details={"path": str(request.url.path)}
    )

    response = StandardResponse(
        status=ResponseStatus.ERROR,
        message="데이터베이스 처리 중 오류 발생",
        data=None,
        errors=[error],
        timestamp=datetime.utcnow()
    )

    logger.error(f"Database error on {request.url.path}: {exc.message}")

    return JSONResponse(
        status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
        content=response.model_dump()
    )
