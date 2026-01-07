"""
Dashboard Router
================

웹 대시보드 UI 및 상태 API 엔드포인트
"""

from fastapi import APIRouter, Request
from fastapi.responses import HTMLResponse
from fastapi.templating import Jinja2Templates
from datetime import datetime
from ..database import db

router = APIRouter(tags=["Dashboard"])

# Jinja2 템플릿 설정
templates = Jinja2Templates(directory="fastapi_server/templates")


@router.get("/", response_class=HTMLResponse, include_in_schema=False)
async def dashboard(request: Request):
    """
    메인 대시보드 페이지
    """
    return templates.TemplateResponse(
        "dashboard.html",
        {
            "request": request,
            "uptime": datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        }
    )


@router.get("/api/dashboard/status")
async def get_dashboard_status():
    """
    대시보드 상태 데이터 API

    Returns:
        - database: 데이터베이스 연결 상태
        - stats: 데이터 통계
        - revit: Revit 연결 정보
        - navisworks: Navisworks 연결 정보
    """

    # 데이터베이스 연결 상태
    db_status = db.connection_status()

    # 통계 데이터 초기화
    stats = {
        "total_objects": 0,
        "model_versions": 0,
        "timeliner_tasks": 0,
        "api_requests_24h": 0
    }

    # 데이터베이스가 연결되어 있으면 통계 조회
    if db_status["connected"]:
        try:
            # 총 객체 수
            result = await db.fetchval("SELECT COUNT(*) FROM objects")
            stats["total_objects"] = result or 0

            # 모델 버전 수
            result = await db.fetchval("SELECT COUNT(DISTINCT model_version) FROM metadata")
            stats["model_versions"] = result or 0

        except Exception as e:
            print(f"통계 조회 실패: {e}")

    return {
        "database": db_status,
        "stats": stats,
        "revit": {
            "connected": False,
            "last_data": None,
            "objects_count": 0
        },
        "navisworks": {
            "connected": False,
            "last_sync": None,
            "tasks_count": 0
        },
        "timestamp": datetime.now().isoformat()
    }


@router.get("/api/dashboard/recent-activity")
async def get_recent_activity(limit: int = 20):
    """
    최근 활동 로그 조회

    Args:
        limit: 반환할 로그 수 (기본: 20)

    Returns:
        최근 활동 로그 리스트
    """

    # TODO: 실제 로그 시스템 구현 시 교체
    activities = [
        {
            "timestamp": datetime.now().isoformat(),
            "message": "시스템 시작",
            "level": "info"
        }
    ]

    return {
        "activities": activities[:limit],
        "total": len(activities)
    }
