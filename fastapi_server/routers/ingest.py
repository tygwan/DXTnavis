from fastapi import APIRouter, HTTPException
from datetime import datetime
import logging
import time

from ..database import db
from ..models.schemas import ExtractedData
from ..models.responses import IngestResponse, ResponseStatus
from ..middleware.error_handler import DatabaseError


router = APIRouter(prefix="/api/v1", tags=["ingest"])
logger = logging.getLogger(__name__)


@router.post("/ingest", response_model=IngestResponse)
async def ingest_payload(payload: ExtractedData):
    """
    BIM 데이터 수집 엔드포인트

    - DXrevit에서 추출한 데이터를 PostgreSQL에 저장
    - 메타데이터, 객체, 관계 데이터를 배치 처리
    - UPSERT 방식으로 중복 방지
    """
    start_time = time.time()
    warnings = []

    try:
        meta = payload.Metadata

        # 입력 검증
        if not meta.ModelVersion or not meta.ModelVersion.strip():
            raise HTTPException(status_code=400, detail="ModelVersion is required")
        if not meta.ProjectName or not meta.ProjectName.strip():
            raise HTTPException(status_code=400, detail="ProjectName is required")
        if not meta.CreatedBy or not meta.CreatedBy.strip():
            raise HTTPException(status_code=400, detail="CreatedBy is required")
        # 1) Upsert metadata
        upsert_meta_sql = (
            "INSERT INTO metadata (model_version, timestamp, project_name, created_by, description, total_object_count, revit_file_path) "
            "VALUES ($1, $2, $3, $4, $5, $6, $7) "
            "ON CONFLICT (model_version) DO UPDATE SET "
            "timestamp = EXCLUDED.timestamp, project_name = EXCLUDED.project_name, created_by = EXCLUDED.created_by, "
            "description = EXCLUDED.description, total_object_count = EXCLUDED.total_object_count, revit_file_path = EXCLUDED.revit_file_path"
        )
        await db.execute(
            upsert_meta_sql,
            (
                meta.ModelVersion,
                meta.Timestamp,
                meta.ProjectName,
                meta.CreatedBy,
                meta.Description,
                meta.TotalObjectCount,
                meta.RevitFilePath,
            ),
        )

        # 2) Upsert objects (batch)
        if payload.Objects:
            upsert_obj_sql = (
                "INSERT INTO objects (model_version, object_id, element_id, category, family, type, activity_id, properties, bounding_box) "
                "VALUES ($1, $2, $3, $4, $5, $6, $7, ($8)::jsonb, ($9)::jsonb) "
                "ON CONFLICT (model_version, object_id) DO UPDATE SET "
                "element_id = EXCLUDED.element_id, category = EXCLUDED.category, family = EXCLUDED.family, type = EXCLUDED.type, "
                "activity_id = EXCLUDED.activity_id, properties = EXCLUDED.properties, bounding_box = EXCLUDED.bounding_box"
            )
            obj_params = [
                (
                    o.ModelVersion,
                    o.ObjectId,
                    o.ElementId,
                    o.Category,
                    o.Family,
                    o.Type,
                    o.ActivityId,
                    o.Properties,
                    o.BoundingBox,
                )
                for o in payload.Objects
            ]
            objects_count = await db.executemany(upsert_obj_sql, obj_params)
        else:
            objects_count = 0

        # 3) Upsert relationships (batch)
        if payload.Relationships:
            upsert_rel_sql = (
                "INSERT INTO relationships (model_version, source_object_id, target_object_id, relation_type, is_directed) "
                "VALUES ($1, $2, $3, $4, $5) "
                "ON CONFLICT (model_version, source_object_id, target_object_id, relation_type) DO UPDATE SET "
                "is_directed = EXCLUDED.is_directed"
            )
            rel_params = [
                (
                    r.ModelVersion,
                    r.SourceObjectId,
                    r.TargetObjectId,
                    r.RelationType,
                    r.IsDirected,
                )
                for r in payload.Relationships
            ]
            relationships_count = await db.executemany(upsert_rel_sql, rel_params)
        else:
            relationships_count = 0

        # 처리 시간 계산
        processing_time = (time.time() - start_time) * 1000  # 밀리초

        # 경고 메시지 생성
        if objects_count == 0:
            warnings.append("No objects were inserted")
        if relationships_count == 0:
            warnings.append("No relationships were inserted")

        logger.info(
            f"Ingest completed: version={meta.ModelVersion}, "
            f"objects={objects_count}, relationships={relationships_count}, "
            f"time={processing_time:.2f}ms"
        )

        return IngestResponse(
            status=ResponseStatus.SUCCESS,
            message="Data ingested successfully",
            model_version=meta.ModelVersion,
            objects_inserted=objects_count,
            relationships_inserted=relationships_count,
            processing_time_ms=processing_time,
            timestamp=datetime.utcnow(),
            warnings=warnings if warnings else None
        )
    except HTTPException:
        raise
    except Exception as e:
        logger.exception("Ingest failed")
        raise DatabaseError(f"Failed to ingest data: {str(e)}", "INGEST_FAILED")
