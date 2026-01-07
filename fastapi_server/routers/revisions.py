"""
리비전 관리 API 엔드포인트
"""
from fastapi import APIRouter, HTTPException, Depends, UploadFile, File
from pydantic import BaseModel, Field
from typing import Optional, List
from datetime import datetime
from uuid import UUID
import hashlib
import json

from ..database import get_db


router = APIRouter(prefix="/api/v1/projects/{project_code}/revisions", tags=["revisions"])


# ============================================
# Pydantic Models
# ============================================

class RevisionCreate(BaseModel):
    """리비전 생성 요청"""
    version_tag: Optional[str] = Field(None, description="버전 태그 (예: v1.0, RC1)")
    description: Optional[str] = Field(None, description="변경 설명")
    source_type: str = Field(..., description="소스 타입 (revit | navisworks)")
    source_file_path: Optional[str] = None
    created_by: str = Field(..., description="생성자")
    metadata: Optional[dict] = Field(default_factory=dict)


class RevisionResponse(BaseModel):
    """리비전 응답"""
    id: UUID
    project_id: UUID
    revision_number: int
    version_tag: Optional[str] = None
    description: Optional[str] = None
    source_type: str
    source_file_path: Optional[str] = None
    source_file_hash: Optional[str] = None
    total_objects: int
    total_categories: int
    revit_objects: int
    navisworks_objects: int
    parent_revision_id: Optional[UUID] = None
    changes_summary: dict
    created_by: str
    created_at: datetime
    metadata: dict

    class Config:
        from_attributes = True


class ObjectBulkCreate(BaseModel):
    """객체 대량 생성 요청"""
    objects: List[dict]


# ============================================
# Utility Functions
# ============================================

def calculate_file_hash(file_path: str) -> str:
    """파일 SHA256 해시 계산"""
    sha256_hash = hashlib.sha256()
    try:
        with open(file_path, "rb") as f:
            for byte_block in iter(lambda: f.read(4096), b""):
                sha256_hash.update(byte_block)
        return sha256_hash.hexdigest()
    except FileNotFoundError:
        return None


# ============================================
# API Endpoints
# ============================================

@router.post("", response_model=RevisionResponse, status_code=201)
async def create_revision(
    project_code: str,
    revision: RevisionCreate,
    db = Depends(get_db)
):
    """
    새 리비전 생성

    - **project_code**: 프로젝트 코드
    - **source_type**: revit | navisworks
    - **created_by**: 생성자 (필수)
    """
    # 1. 프로젝트 존재 확인
    project = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code
    )
    if not project:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    project_id = project['id']

    # 2. 다음 리비전 번호 가져오기
    revision_number = await db.fetchval(
        "SELECT get_next_revision_number($1, $2)",
        project_id, revision.source_type
    )

    # 3. 파일 해시 계산 (선택적)
    file_hash = None
    if revision.source_file_path:
        file_hash = calculate_file_hash(revision.source_file_path)

    # 4. 리비전 생성
    revision_id = await db.fetchval("""
        INSERT INTO revisions (
            project_id, revision_number, version_tag, description,
            source_type, source_file_path, source_file_hash,
            created_by, metadata
        ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9::jsonb)
        RETURNING id
    """,
        project_id, revision_number, revision.version_tag, revision.description,
        revision.source_type, revision.source_file_path, file_hash,
        revision.created_by, json.dumps(revision.metadata)
    )

    # 5. 생성된 리비전 반환
    result = await db.fetchrow("SELECT * FROM revisions WHERE id = $1", revision_id)
    return dict(result)


@router.get("", response_model=List[RevisionResponse])
async def list_revisions(
    project_code: str,
    source_type: Optional[str] = None,
    limit: int = 50,
    db = Depends(get_db)
):
    """
    리비전 목록 조회

    - **project_code**: 프로젝트 코드
    - **source_type**: 필터 (revit | navisworks)
    - **limit**: 최대 결과 수
    """
    # 1. 프로젝트 ID 조회
    project = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code
    )
    if not project:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    # 2. 리비전 목록 조회
    revisions = await db.fetch("""
        SELECT * FROM revisions
        WHERE project_id = $1
          AND ($2::text IS NULL OR source_type = $2)
        ORDER BY revision_number DESC
        LIMIT $3
    """, project['id'], source_type, limit)

    return [dict(r) for r in revisions]


@router.get("/{revision_number}", response_model=RevisionResponse)
async def get_revision(
    project_code: str,
    revision_number: int,
    source_type: str = "revit",
    db = Depends(get_db)
):
    """
    특정 리비전 상세 조회

    - **project_code**: 프로젝트 코드
    - **revision_number**: 리비전 번호
    - **source_type**: revit | navisworks (기본: revit)
    """
    # 1. 프로젝트 ID 조회
    project = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code
    )
    if not project:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    # 2. 리비전 조회
    revision = await db.fetchrow("""
        SELECT * FROM revisions
        WHERE project_id = $1
          AND revision_number = $2
          AND source_type = $3
    """, project['id'], revision_number, source_type)

    if not revision:
        raise HTTPException(
            404,
            f"리비전 #{revision_number} ({source_type})를 찾을 수 없습니다"
        )

    return dict(revision)


@router.get("/latest/{source_type}", response_model=RevisionResponse)
async def get_latest_revision(
    project_code: str,
    source_type: str,
    db = Depends(get_db)
):
    """
    최신 리비전 조회

    - **project_code**: 프로젝트 코드
    - **source_type**: revit | navisworks
    """
    # 1. 프로젝트 ID 조회
    project = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code
    )
    if not project:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    # 2. 최신 리비전 조회
    revision = await db.fetchrow("""
        SELECT * FROM revisions
        WHERE project_id = $1
          AND source_type = $2
        ORDER BY revision_number DESC
        LIMIT 1
    """, project['id'], source_type)

    if not revision:
        raise HTTPException(
            404,
            f"프로젝트 '{project_code}'의 {source_type} 리비전이 없습니다"
        )

    return dict(revision)


@router.post("/{revision_number}/objects/bulk", status_code=201)
async def bulk_create_objects(
    project_code: str,
    revision_number: int,
    source_type: str,
    data: ObjectBulkCreate,
    db = Depends(get_db)
):
    """
    객체 대량 생성

    - **project_code**: 프로젝트 코드
    - **revision_number**: 리비전 번호
    - **source_type**: revit | navisworks
    - **data**: 객체 목록
    """
    # 1. 프로젝트 및 리비전 확인
    project = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code
    )
    if not project:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    revision = await db.fetchrow("""
        SELECT id FROM revisions
        WHERE project_id = $1
          AND revision_number = $2
          AND source_type = $3
    """, project['id'], revision_number, source_type)

    if not revision:
        raise HTTPException(
            404,
            f"리비전 #{revision_number} ({source_type})를 찾을 수 없습니다"
        )

    # 2. 객체 대량 삽입
    async with db.transaction():
        inserted_count = 0
        for obj in data.objects:
            try:
                await db.execute("""
                    INSERT INTO unified_objects (
                        project_id, revision_id, object_id, element_id,
                        source_type, parent_object_id, level, display_name,
                        category, family, type, activity_id,
                        properties, bounding_box, spatial_path
                    ) VALUES (
                        $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13::jsonb, $14::jsonb, $15
                    )
                    ON CONFLICT (revision_id, object_id, source_type) DO NOTHING
                """,
                    project['id'],
                    revision['id'],
                    obj['object_id'],
                    obj.get('element_id'),
                    source_type,
                    obj.get('parent_object_id'),
                    obj.get('level', 0),
                    obj.get('display_name'),
                    obj.get('category'),
                    obj.get('family'),
                    obj.get('type'),
                    obj.get('activity_id'),
                    json.dumps(obj.get('properties', {})),
                    json.dumps(obj.get('bounding_box')) if obj.get('bounding_box') else None,
                    obj.get('spatial_path')
                )
                inserted_count += 1
            except Exception as e:
                print(f"객체 삽입 실패: {e}")

        # 3. 리비전 통계 업데이트
        await db.execute("""
            UPDATE revisions
            SET total_objects = (
                SELECT COUNT(*) FROM unified_objects WHERE revision_id = $1
            )
            WHERE id = $1
        """, revision['id'])

    return {
        "message": f"{inserted_count}개 객체 생성 완료",
        "total_objects": inserted_count
    }


@router.get("/{revision_number}/objects")
async def get_revision_objects(
    project_code: str,
    revision_number: int,
    source_type: str = "revit",
    category: Optional[str] = None,
    limit: int = 100,
    offset: int = 0,
    db = Depends(get_db)
):
    """
    리비전의 객체 목록 조회

    - **project_code**: 프로젝트 코드
    - **revision_number**: 리비전 번호
    - **source_type**: revit | navisworks
    - **category**: 카테고리 필터
    """
    # 1. 프로젝트 및 리비전 확인
    project = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code
    )
    if not project:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    revision = await db.fetchrow("""
        SELECT id FROM revisions
        WHERE project_id = $1
          AND revision_number = $2
          AND source_type = $3
    """, project['id'], revision_number, source_type)

    if not revision:
        raise HTTPException(
            404,
            f"리비전 #{revision_number} ({source_type})를 찾을 수 없습니다"
        )

    # 2. 객체 조회
    objects = await db.fetch("""
        SELECT * FROM unified_objects
        WHERE revision_id = $1
          AND ($2::text IS NULL OR category = $2)
        ORDER BY id
        LIMIT $3 OFFSET $4
    """, revision['id'], category, limit, offset)

    return [dict(obj) for obj in objects]


@router.delete("/{revision_number}")
async def delete_revision(
    project_code: str,
    revision_number: int,
    source_type: str,
    db = Depends(get_db)
):
    """
    리비전 삭제 (CASCADE로 관련 객체도 삭제됨)

    - **project_code**: 프로젝트 코드
    - **revision_number**: 리비전 번호
    - **source_type**: revit | navisworks
    """
    # 1. 프로젝트 ID 조회
    project = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code
    )
    if not project:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    # 2. 리비전 삭제
    deleted = await db.execute("""
        DELETE FROM revisions
        WHERE project_id = $1
          AND revision_number = $2
          AND source_type = $3
    """, project['id'], revision_number, source_type)

    if deleted == "DELETE 0":
        raise HTTPException(
            404,
            f"리비전 #{revision_number} ({source_type})를 찾을 수 없습니다"
        )

    return {
        "message": f"리비전 #{revision_number} ({source_type}) 삭제 완료"
    }
