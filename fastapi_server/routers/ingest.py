"""
DXrevit snapshot ingestion endpoint.
"""

from __future__ import annotations

import json
import logging
import os
import time
import uuid
from datetime import datetime
from typing import Any, Dict, List, Optional, Tuple

from fastapi import APIRouter, HTTPException

from ..database import db
from ..middleware.error_handler import DatabaseError
from ..models.responses import IngestResponse, ResponseStatus
from ..models.schemas import (
    ExtractedData,
    MetadataRecord,
    ObjectRecord,
    IngestRequestV2,
    IngestResponseV2,
    UnifiedObjectDto,
)
from ..utils.identifiers import (
    extract_ifc_guid,
    generate_project_code,
    sanitize_display_name,
    to_deterministic_uuid,
)
from ..utils.backward_compat import migrate_legacy_batch
from ..utils.db_helpers import ensure_project_and_revision


router = APIRouter(prefix="/api/v1", tags=["ingest"])
logger = logging.getLogger(__name__)


async def _ensure_project(meta: MetadataRecord) -> Tuple[uuid.UUID, str]:
    project_code = generate_project_code(meta.ProjectName)
    existing = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code,
    )

    revit_file_name = os.path.basename(meta.RevitFilePath) if meta.RevitFilePath else None

    if existing:
        project_id = existing["id"]
        await db.execute(
            """
            UPDATE projects
            SET revit_file_name = COALESCE($2, revit_file_name),
                revit_file_path = COALESCE($3, revit_file_path),
                updated_at = NOW()
            WHERE id = $1
            """,
            (project_id, revit_file_name, meta.RevitFilePath),
        )
        return project_id, project_code

    project_id = await db.fetchval(
        """
        INSERT INTO projects (
            code, name, revit_file_name, revit_file_path,
            created_by, metadata
        )
        VALUES ($1, $2, $3, $4, $5, jsonb_build_object('source', 'dxrevit_ingest'))
        RETURNING id
        """,
        (
            project_code,
            meta.ProjectName,
            revit_file_name,
            meta.RevitFilePath,
            meta.CreatedBy,
        ),
    )

    logger.info("Created project %s (id=%s)", project_code, project_id)
    return project_id, project_code


async def _ensure_revision(project_id: uuid.UUID, meta: MetadataRecord) -> uuid.UUID:
    existing = await db.fetchrow(
        """
        SELECT r.id
        FROM revision_versions rv
        JOIN revisions r ON rv.revision_id = r.id
        WHERE rv.model_version = $1
        """,
        meta.ModelVersion,
    )

    total_objects = meta.TotalObjectCount or 0

    if existing:
        revision_id = existing["id"]
        await db.execute(
            """
            UPDATE revisions
            SET description = COALESCE($2, description),
                source_file_path = COALESCE($3, source_file_path),
                total_objects = GREATEST($4, total_objects),
                revit_objects = GREATEST($4, revit_objects),
                metadata = metadata || jsonb_build_object('last_ingested_at', NOW())
            WHERE id = $1
            """,
            (
                revision_id,
                meta.Description,
                meta.RevitFilePath,
                total_objects,
            ),
        )
        await db.execute(
            """
            UPDATE revision_versions
            SET source_type = 'revit',
                source_file_path = COALESCE($2, source_file_path),
                extracted_at = NOW()
            WHERE model_version = $1
            """,
            (
                meta.ModelVersion,
                meta.RevitFilePath,
            ),
        )
        return revision_id

    revision_number = await db.fetchval(
        "SELECT COALESCE(MAX(revision_number), 0) + 1 FROM revisions WHERE project_id = $1",
        project_id,
    )

    revision_id = await db.fetchval(
        """
        INSERT INTO revisions (
            project_id, revision_number, model_version, description,
            source_type, source_file_path, total_objects, revit_objects,
            created_by, metadata
        )
        VALUES ($1, $2, $3, $4, 'revit', $5, $6, $6, $7,
                jsonb_build_object('ingested_at', NOW()))
        RETURNING id
        """,
        (
            project_id,
            revision_number,
            meta.ModelVersion,
            meta.Description,
            meta.RevitFilePath,
            total_objects,
            meta.CreatedBy,
        ),
    )

    await db.execute(
        """
        INSERT INTO revision_versions (model_version, revision_id, source_type, source_file_path)
        VALUES ($1, $2, 'revit', $3)
        ON CONFLICT (model_version) DO UPDATE
        SET revision_id = EXCLUDED.revision_id,
            source_type = 'revit',
            source_file_path = COALESCE(EXCLUDED.source_file_path, revision_versions.source_file_path),
            extracted_at = NOW()
        """,
        (
            meta.ModelVersion,
            revision_id,
            meta.RevitFilePath,
        ),
    )

    logger.info("Created revision %s for project %s", revision_id, project_id)
    return revision_id


def _prepare_object_payload(
    meta: MetadataRecord,
    obj: ObjectRecord,
) -> Tuple[str, uuid.UUID, Optional[str], Optional[str], str]:
    props: Dict[str, Any] = {}
    if obj.Properties:
        try:
            props = json.loads(obj.Properties)
        except json.JSONDecodeError:
            logger.warning("Invalid properties JSON for object %s", obj.ObjectId)
            props = {}

    props.setdefault("RevitUniqueId", obj.ObjectId)
    props.setdefault("ElementId", obj.ElementId)
    props.setdefault("ProjectName", meta.ProjectName)
    props.setdefault("ModelVersion", meta.ModelVersion)

    display_name = sanitize_display_name(
        props.get("Name"),
        obj.Type,
        obj.Family,
        obj.Category,
    )
    category = obj.Category or props.get("Category") or "UNKNOWN"
    props_json = json.dumps(props, ensure_ascii=False)
    object_uuid = to_deterministic_uuid(obj.ObjectId)
    ifc_guid = extract_ifc_guid(props)

    return props_json, object_uuid, ifc_guid, display_name, category


@router.post("/ingest")
async def ingest_payload(payload: Dict[str, Any]):
    """
    Ingest BIM data into unified schema with automatic format detection.

    Supports two payload formats:
    1. Legacy ExtractedData (DXrevit): Has 'Metadata' field
    2. Phase B IngestRequestV2: Has 'project_code' field

    This enables seamless backward compatibility while supporting new dual-identity schema.
    """
    # Auto-detect payload format
    if "Metadata" in payload:
        # Legacy DXrevit payload - route to original handler
        return await _ingest_legacy(ExtractedData(**payload))
    elif "project_code" in payload:
        # Phase B payload - route to v2 handler
        request = IngestRequestV2(**payload)
        return await _ingest_v2(request)
    else:
        raise HTTPException(
            status_code=400,
            detail="Invalid payload format. Expected either 'Metadata' (legacy) or 'project_code' (v2) field."
        )


async def _ingest_legacy(payload: ExtractedData) -> IngestResponse:
    """
    Original DXrevit snapshot ingestion (backward compatibility).
    """
    start_time = time.time()
    warnings: List[str] = []

    try:
        meta = payload.Metadata

        if not meta.ModelVersion or not meta.ModelVersion.strip():
            raise HTTPException(status_code=400, detail="ModelVersion is required")
        if not meta.ProjectName or not meta.ProjectName.strip():
            raise HTTPException(status_code=400, detail="ProjectName is required")
        if not meta.CreatedBy or not meta.CreatedBy.strip():
            raise HTTPException(status_code=400, detail="CreatedBy is required")

        project_id, project_code = await _ensure_project(meta)
        revision_id = await _ensure_revision(project_id, meta)

        await db.execute(
            """
            INSERT INTO metadata (model_version, timestamp, project_name, created_by, description, total_object_count, revit_file_path)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            ON CONFLICT (model_version) DO UPDATE SET
                timestamp = EXCLUDED.timestamp,
                project_name = EXCLUDED.project_name,
                created_by = EXCLUDED.created_by,
                description = EXCLUDED.description,
                total_object_count = EXCLUDED.total_object_count,
                revit_file_path = EXCLUDED.revit_file_path
            """,
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

        objects_count = 0
        unified_count = 0

        if payload.Objects:
            upsert_obj_sql = (
                "INSERT INTO objects (model_version, object_id, element_id, category, family, type, activity_id, properties, bounding_box) "
                "VALUES ($1, $2, $3, $4, $5, $6, $7, ($8)::jsonb, ($9)::jsonb) "
                "ON CONFLICT (model_version, object_id) DO UPDATE SET "
                "element_id = EXCLUDED.element_id, category = EXCLUDED.category, family = EXCLUDED.family, type = EXCLUDED.type, "
                "activity_id = EXCLUDED.activity_id, properties = EXCLUDED.properties, bounding_box = EXCLUDED.bounding_box"
            )

            unified_upsert_sql = """
                INSERT INTO unified_objects (
                    project_id, revision_id, object_id, element_id, source_type,
                    parent_object_id, level, display_name, spatial_path,
                    category, family, type, activity_id,
                    properties, bounding_box, change_type, ifc_guid, navisworks_object_id
                )
                VALUES (
                    $1, $2, $3, $4, 'revit',
                    NULL, 0, $5, NULL,
                    $6, $7, $8, $9,
                    ($10)::jsonb, ($11)::jsonb, 'added', $12, NULL
                )
                ON CONFLICT (revision_id, object_id, source_type) DO UPDATE SET
                    element_id = EXCLUDED.element_id,
                    display_name = COALESCE(EXCLUDED.display_name, unified_objects.display_name),
                    category = EXCLUDED.category,
                    family = EXCLUDED.family,
                    type = EXCLUDED.type,
                    activity_id = EXCLUDED.activity_id,
                    properties = unified_objects.properties || EXCLUDED.properties,
                    bounding_box = COALESCE(EXCLUDED.bounding_box, unified_objects.bounding_box),
                    ifc_guid = COALESCE(EXCLUDED.ifc_guid, unified_objects.ifc_guid)
            """

            object_rows: List[Tuple[Any, ...]] = []
            unified_rows: List[Tuple[Any, ...]] = []

            for obj in payload.Objects:
                props_json, object_uuid, ifc_guid, display_name, category = _prepare_object_payload(meta, obj)

                object_rows.append(
                    (
                        meta.ModelVersion,
                        obj.ObjectId,
                        obj.ElementId,
                        obj.Category,
                        obj.Family,
                        obj.Type,
                        obj.ActivityId,
                        props_json,
                        obj.BoundingBox,
                    )
                )

                unified_rows.append(
                    (
                        project_id,
                        revision_id,
                        object_uuid,
                        obj.ElementId,
                        display_name,
                        category,
                        obj.Family,
                        obj.Type,
                        obj.ActivityId,
                        props_json,
                        obj.BoundingBox,
                        ifc_guid,
                    )
                )

            objects_count = await db.executemany(upsert_obj_sql, object_rows)
            unified_count = await db.executemany(unified_upsert_sql, unified_rows)

            if objects_count:
                await db.execute(
                    """
                    UPDATE revisions
                    SET total_objects = $2,
                        revit_objects = $2
                    WHERE id = $1
                    """,
                    (revision_id, objects_count),
                )
        else:
            warnings.append("No objects were provided in payload")

        if payload.Relationships:
            upsert_rel_sql = (
                "INSERT INTO relationships (model_version, source_object_id, target_object_id, relation_type, is_directed) "
                "VALUES ($1, $2, $3, $4, $5) "
                "ON CONFLICT (model_version, source_object_id, target_object_id, relation_type) DO UPDATE SET "
                "is_directed = EXCLUDED.is_directed"
            )
            rel_rows = [
                (
                    relation.ModelVersion,
                    relation.SourceObjectId,
                    relation.TargetObjectId,
                    relation.RelationType,
                    relation.IsDirected,
                )
                for relation in payload.Relationships
            ]
            relationships_count = await db.executemany(upsert_rel_sql, rel_rows)
        else:
            relationships_count = 0
            warnings.append("No relationships were provided in payload")

        processing_time = (time.time() - start_time) * 1000

        logger.info(
            "DXrevit ingest completed: project=%s revision=%s version=%s objects=%d (unified=%d) relationships=%d elapsed=%.2fms",
            project_code,
            revision_id,
            meta.ModelVersion,
            objects_count,
            unified_count,
            relationships_count,
            processing_time,
        )

        return IngestResponse(
            status=ResponseStatus.SUCCESS,
            message="Data ingested successfully",
            model_version=meta.ModelVersion,
            objects_inserted=objects_count,
            relationships_inserted=relationships_count,
            processing_time_ms=processing_time,
            timestamp=datetime.utcnow(),
            warnings=warnings if warnings else None,
        )
    except HTTPException:
        raise
    except Exception as exc:  # pragma: no cover - defensive logging
        logger.exception("DXrevit ingest failed")
        raise DatabaseError(f"Failed to ingest data: {exc}", "INGEST_FAILED")


async def _ingest_v2(request: IngestRequestV2) -> IngestResponseV2:
    """
    Phase B ingestion with dual-identity schema (AWP 2025 v1.1.0).

    Features:
    - Supports both legacy (unique_id) and modern (unique_key + object_guid) payloads
    - Automatic project and revision creation
    - Upsert by (revision_id, source_type, unique_key) constraint
    - Batch processing with executemany for performance

    Phase B requirements (techspec.md §2):
    - Accept unique_key (primary) and object_guid (optional)
    - Backward compatible with unique_id field
    - No duplicate objects on repeated ingestion
    """
    start_time = time.time()

    try:
        # Validate request
        if not request.objects:
            raise HTTPException(status_code=400, detail="No objects provided")

        # Migrate legacy objects (unique_id → unique_key)
        migrated_objects = migrate_legacy_batch(request.objects)

        # Get or create project
        project_id = await db.fetchval(
            "SELECT id FROM projects WHERE code = $1",
            (request.project_code,)
        )

        if not project_id:
            project_id = await db.fetchval(
                """
                INSERT INTO projects (code, name, created_by)
                VALUES ($1, $2, 'system')
                RETURNING id
                """,
                (request.project_code, request.project_code)
            )

        # Get or create revision
        revision_id = await db.fetchval(
            "SELECT id FROM revisions WHERE project_id = $1 AND revision_number = $2",
            (project_id, request.revision_number)
        )

        if not revision_id:
            revision_id = await db.fetchval(
                """
                INSERT INTO revisions (project_id, revision_number, source_type, created_by)
                VALUES ($1, $2, $3, 'system')
                RETURNING id
                """,
                (project_id, request.revision_number, request.source_type)
            )

        # Prepare batch upsert SQL
        # ON CONFLICT uses the new constraint: (revision_id, source_type, unique_key)
        upsert_sql = """
            INSERT INTO unified_objects (
                project_id, revision_id, unique_key, object_guid,
                category, display_name, source_type, properties, geometry,
                updated_at
            )
            VALUES (
                $1, $2, $3, $4,
                $5, $6, $7, $8::jsonb, $9::jsonb,
                NOW()
            )
            ON CONFLICT (revision_id, source_type, unique_key) DO UPDATE SET
                object_guid = COALESCE(EXCLUDED.object_guid, unified_objects.object_guid),
                category = EXCLUDED.category,
                display_name = EXCLUDED.display_name,
                properties = unified_objects.properties || EXCLUDED.properties,
                geometry = COALESCE(EXCLUDED.geometry, unified_objects.geometry),
                updated_at = NOW()
        """

        # Prepare batch rows
        batch_rows = []
        for obj in migrated_objects:
            # Validate that unique_key exists after migration
            if not obj.unique_key:
                logger.warning(
                    "Object missing unique_key after migration: %s",
                    obj.model_dump()
                )
                continue

            # Convert properties dict to JSON string
            props_json = json.dumps(obj.properties, ensure_ascii=False)
            geometry_json = json.dumps(obj.geometry, ensure_ascii=False) if obj.geometry else None

            batch_rows.append((
                project_id,
                revision_id,
                obj.unique_key,
                obj.object_guid,
                obj.category,
                obj.name,
                obj.source_type,
                props_json,
                geometry_json,
            ))

        # Execute batch upsert
        if batch_rows:
            await db.executemany(upsert_sql, batch_rows)
            object_count = len(batch_rows)
        else:
            object_count = 0

        processing_time = (time.time() - start_time) * 1000

        logger.info(
            "Phase B ingest completed: project_code=%s revision=%s objects=%d elapsed=%.2fms",
            request.project_code,
            request.revision_number,
            object_count,
            processing_time,
        )

        return IngestResponseV2(
            success=True,
            revision_id=str(revision_id),
            object_count=object_count,
            message=f"Ingested {object_count} objects successfully"
        )

    except HTTPException:
        raise
    except Exception as exc:
        logger.exception("Phase B ingest failed")
        raise DatabaseError(f"Failed to ingest data: {exc}", "INGEST_V2_FAILED")
