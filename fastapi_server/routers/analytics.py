from fastapi import APIRouter, HTTPException, Query
from typing import Optional, List, Dict, Any
import logging

from ..database import db
from ..models.responses import StandardResponse, PaginatedResponse, ResponseStatus
from ..middleware.error_handler import DatabaseError


router = APIRouter(prefix="/api/v1", tags=["analytics"])
logger = logging.getLogger(__name__)


@router.get("/models/versions", response_model=PaginatedResponse)
async def get_versions(
    projectName: Optional[str] = Query(None, description="프로젝트 이름으로 필터링"),
    page: int = Query(1, ge=1, description="페이지 번호 (1부터 시작)"),
    page_size: int = Query(100, ge=1, le=1000, description="페이지당 항목 수"),
    sort: str = Query("timestamp:desc", regex="^(timestamp|model_version):(asc|desc)$", description="정렬 (예: timestamp:desc)")
):
    """
    모델 버전 목록 조회 (페이지네이션 지원)

    - 프로젝트별 필터링 가능
    - timestamp 또는 model_version 기준 정렬
    """
    try:
        offset = (page - 1) * page_size
        sort_field, sort_order = sort.split(":")

        # 총 개수 조회
        if projectName:
            count_row = await db.fetch(
                "SELECT COUNT(*) as total FROM metadata WHERE project_name = $1",
                (projectName,)
            )
            rows = await db.fetch(
                f"SELECT model_version, timestamp, project_name, created_by FROM metadata "
                f"WHERE project_name = $1 ORDER BY {sort_field} {sort_order.upper()} LIMIT $2 OFFSET $3",
                (projectName, page_size, offset),
            )
        else:
            count_row = await db.fetch("SELECT COUNT(*) as total FROM metadata")
            rows = await db.fetch(
                f"SELECT model_version, timestamp, project_name, created_by FROM metadata "
                f"ORDER BY {sort_field} {sort_order.upper()} LIMIT $1 OFFSET $2",
                (page_size, offset),
            )

        total = count_row[0]["total"]
        total_pages = (total + page_size - 1) // page_size

        return PaginatedResponse(
            status=ResponseStatus.SUCCESS,
            message="Versions retrieved successfully",
            data=[dict(r) for r in rows],
            pagination={
                "total": total,
                "page": page,
                "page_size": page_size,
                "total_pages": total_pages
            }
        )
    except Exception as e:
        logger.exception("Failed to get versions")
        raise DatabaseError(f"Failed to retrieve versions: {str(e)}", "QUERY_FAILED")


@router.get("/models/{version}/summary", response_model=StandardResponse)
async def get_version_summary(version: str):
    """
    특정 버전의 요약 정보 조회

    - 카테고리별 객체 수
    - 4D 연결 가능 객체 수
    - 총 관계 수 등
    """
    try:
        rows = await db.fetch(
            "SELECT * FROM analytics_version_summary WHERE model_version = $1",
            (version,),
        )
        if not rows:
            raise HTTPException(
                status_code=404,
                detail=f"Summary not found for version: {version}"
            )

        return StandardResponse(
            status=ResponseStatus.SUCCESS,
            message=f"Summary retrieved for version {version}",
            data=dict(rows[0])
        )
    except HTTPException:
        raise
    except Exception as e:
        logger.exception(f"Failed to get summary for {version}")
        raise DatabaseError(f"Failed to retrieve summary: {str(e)}", "QUERY_FAILED")


@router.get("/models/compare", response_model=PaginatedResponse)
async def compare_versions(
    v1: str = Query(..., description="비교 기준 버전"),
    v2: str = Query(..., description="비교 대상 버전"),
    changeType: Optional[str] = Query(None, regex="^(ADDED|DELETED|MODIFIED)$", description="변경 유형 필터"),
    page: int = Query(1, ge=1, description="페이지 번호"),
    page_size: int = Query(1000, ge=1, le=10000, description="페이지당 항목 수")
):
    """
    두 버전 간 변경 사항 비교

    - 추가(ADDED), 삭제(DELETED), 수정(MODIFIED) 객체 조회
    - 변경 유형별 필터링 가능
    """
    try:
        offset = (page - 1) * page_size

        # 총 개수 조회
        if changeType:
            count_row = await db.fetch(
                "SELECT COUNT(*) as total FROM fn_compare_versions($1, $2) WHERE change_type = $3",
                (v1, v2, changeType)
            )
            rows = await db.fetch(
                "SELECT * FROM fn_compare_versions($1, $2) WHERE change_type = $3 LIMIT $4 OFFSET $5",
                (v1, v2, changeType, page_size, offset),
            )
        else:
            count_row = await db.fetch(
                "SELECT COUNT(*) as total FROM fn_compare_versions($1, $2)",
                (v1, v2,)
            )
            rows = await db.fetch(
                "SELECT * FROM fn_compare_versions($1, $2) LIMIT $3 OFFSET $4",
                (v1, v2, page_size, offset),
            )

        total = count_row[0]["total"]
        total_pages = (total + page_size - 1) // page_size

        return PaginatedResponse(
            status=ResponseStatus.SUCCESS,
            message=f"Comparison between {v1} and {v2} retrieved successfully",
            data=[dict(r) for r in rows],
            pagination={
                "total": total,
                "page": page,
                "page_size": page_size,
                "total_pages": total_pages
            }
        )
    except Exception as e:
        logger.exception(f"Failed to compare {v1} and {v2}")
        raise DatabaseError(f"Failed to compare versions: {str(e)}", "QUERY_FAILED")
