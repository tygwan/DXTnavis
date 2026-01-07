"""
Navisworks 데이터 업로드 및 조회 라우터
"""

from __future__ import annotations

import csv
import io
import json
from typing import Any, Dict, List, Optional, Sequence

from fastapi import APIRouter, Depends, File, HTTPException, UploadFile

from ..database import get_db
from ..utils.identifiers import sanitize_display_name, to_deterministic_uuid


router = APIRouter(prefix="/api/v1/navisworks", tags=["navisworks"])


# ============================================
# Utility helpers
# ============================================

IFC_GUID_KEYS = {"IfcGUID", "IFC Guid", "IFC_GUID"}
SOURCE_PATH_KEYS = {
    "소스 파일 이름",
    "소스 파일 경로",
    "Source File Name",
    "Source File Path",
}


def sanitize_property_value(value: Optional[str]) -> Optional[str]:
    if value is None:
        return None
    value = value.strip()
    for prefix in ("DisplayString:", "NamedConstant:", "Boolean:", "Double:", "Integer:"):
        if value.startswith(prefix):
            return value[len(prefix) :].strip()
    return value


def aggregate_eav_properties(csv_rows: List[dict]) -> tuple[List[dict], List[str], List[str], Optional[str]]:
    aggregated: Dict[str, Dict[str, Any]] = {}
    object_ids: List[str] = []
    ifc_guids: List[str] = []
    source_hint: Optional[str] = None

    for row in csv_rows:
        obj_id = row.get("ObjectId")
        if not obj_id:
            continue

        if obj_id not in aggregated:
            aggregated[obj_id] = {
                "object_id": obj_id,
                "parent_object_id": row.get("ParentId"),
                "level": int(row.get("Level") or 0),
                "display_name": row.get("DisplayName", ""),
                "category": row.get("Category", ""),
                "properties": {},
            }
            if obj_id not in object_ids and obj_id != "00000000-0000-0000-0000-000000000000":
                object_ids.append(obj_id)

        prop_name = (row.get("PropertyName") or "").strip()
        prop_value = sanitize_property_value(row.get("PropertyValue"))
        if not prop_name or prop_value is None:
            continue

        aggregated[obj_id]["properties"][prop_name] = prop_value

        if prop_name in IFC_GUID_KEYS and prop_value and prop_value not in ifc_guids:
            ifc_guids.append(prop_value)

        if source_hint is None and prop_name in SOURCE_PATH_KEYS and prop_value:
            source_hint = prop_value

    aggregated_list = list(aggregated.values())
    for obj in aggregated_list:
        obj["display_name"] = sanitize_display_name(
            obj.get("display_name"),
            obj["properties"].get("Name"),
        )

    return aggregated_list, object_ids, ifc_guids, source_hint


async def detect_revision_by_evidence(
    db,
    object_ids: Sequence[str],
    ifc_guids: Sequence[str],
    source_hint: Optional[str],
    limit: int = 5,
) -> List[Dict[str, Any]]:
    params: List[Any] = []
    conditions: List[str] = []

    if object_ids:
        params.append([str(to_deterministic_uuid(oid)) for oid in object_ids[:200]])
        conditions.append(f"uo.object_id = ANY(${len(params)}::uuid[])")
    if ifc_guids:
        params.append(ifc_guids[:200])
        conditions.append(f"uo.ifc_guid = ANY(${len(params)}::text[])")
    if source_hint:
        params.append(source_hint)
        placeholder = len(params)
        conditions.append(
            f"(rv.source_file_path = ${placeholder} OR r.source_file_path = ${placeholder})"
        )

    if not conditions:
        return []

    where_clause = " OR ".join(f"({clause})" for clause in conditions)
    params.append(limit)

    rows = await db.fetch(
        f"""
        SELECT
            p.id AS project_id,
            p.code,
            p.name,
            r.id AS revision_id,
            r.revision_number,
            r.source_type,
            r.total_objects,
            COUNT(DISTINCT uo.object_id) AS match_count
        FROM unified_objects uo
        JOIN revisions r ON uo.revision_id = r.id
        JOIN projects p ON r.project_id = p.id
        LEFT JOIN revision_versions rv ON rv.revision_id = r.id
        WHERE {where_clause}
        GROUP BY p.id, p.code, p.name, r.id, r.revision_number, r.source_type, r.total_objects
        ORDER BY COUNT(DISTINCT uo.object_id) DESC, r.revision_number DESC NULLS LAST
        LIMIT ${len(params)}
        """,
        tuple(params),
    )

    detected: List[Dict[str, Any]] = []
    for row in rows:
        total_objects = row["total_objects"] or 0
        match_count = row["match_count"]
        denominator = total_objects if total_objects else match_count
        confidence = float(match_count / denominator) if denominator else 0.0
        detected.append(
            {
                "project_id": row["project_id"],
                "project_code": row["code"],
                "project_name": row["name"],
                "revision_id": row["revision_id"],
                "revision_number": row["revision_number"],
                "source_type": row["source_type"],
                "match_count": match_count,
                "total_objects": total_objects,
                "confidence": round(confidence, 4),
            }
        )
    return detected


# ============================================
# API endpoints
# ============================================

@router.post("/projects/{project_code}/revisions/{revision_number}/hierarchy")
async def upload_hierarchy_csv(
    project_code: str,
    revision_number: int,
    file: UploadFile = File(...),
    db=Depends(get_db),
):
    """Navisworks 계층 데이터 업로드(CSV)"""
    try:
        contents = await file.read()
        try:
            decoded = contents.decode("utf-8-sig")
        except UnicodeDecodeError:
            decoded = contents.decode("euc-kr")
        csv_reader = csv.DictReader(io.StringIO(decoded))
        csv_rows = list(csv_reader)
        if not csv_rows:
            raise HTTPException(400, "CSV 파일이 비어 있습니다")
    except Exception as exc:
        raise HTTPException(400, f"CSV 파일 읽기 실패: {exc}")

    aggregated_objects, object_ids, ifc_guids, source_hint = aggregate_eav_properties(csv_rows)

    project = await db.fetchrow(
        "SELECT id, code, name FROM projects WHERE code = $1",
        project_code,
    )
    detection_result: Optional[Dict[str, Any]] = None

    if not project:
        project = await db.fetchrow(
            "SELECT id, code, name FROM projects WHERE name = $1",
            project_code,
        )

    if not project:
        detected = await detect_revision_by_evidence(db, object_ids, ifc_guids, source_hint, limit=1)
        if detected:
            detection_result = detected[0]
            project = await db.fetchrow(
                "SELECT id, code, name FROM projects WHERE id = $1",
                detection_result["project_id"],
            )
            project_code = detection_result["project_code"]
            revision_number = detection_result["revision_number"]

    if not project:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    revision = await db.fetchrow(
        """
        SELECT id, revision_number, source_type
        FROM revisions
        WHERE project_id = $1 AND revision_number = $2
        """,
        project["id"],
        revision_number,
    )

    if not revision:
        raise HTTPException(
            404,
            f"리비전 #{revision_number}를 찾을 수 없습니다. 먼저 Revit 스냅샷을 업로드하거나 리비전을 생성해 주세요.",
        )

    revision_id = revision["id"]

    model_version_row = await db.fetchrow(
        """
        SELECT model_version, source_file_path, source_type
        FROM revision_versions
        WHERE revision_id = $1
        ORDER BY extracted_at DESC
        LIMIT 1
        """,
        revision_id,
    )
    model_version = model_version_row["model_version"] if model_version_row else None
    source_path = source_hint or (model_version_row["source_file_path"] if model_version_row else None)

    insert_rows = []
    for row in csv_rows:
        property_value = sanitize_property_value(row.get("PropertyValue"))
        ifc_guid = property_value if (row.get("PropertyName") or "") in IFC_GUID_KEYS else None
        insert_rows.append(
            (
                row.get("ObjectId"),
                row.get("ParentId"),
                int(row.get("Level") or 0),
                row.get("DisplayName"),
                row.get("Category"),
                row.get("PropertyName"),
                property_value,
                model_version,
                project["id"],
                revision_id,
                ifc_guid,
                source_path,
                "navisworks",
            )
        )

    await db.executemany(
        """
        INSERT INTO navisworks_hierarchy (
            object_id, parent_id, level, display_name, category,
            property_name, property_value, model_version, project_id,
            revision_id, ifc_guid, source_file_path, source_system
        )
        VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
        ON CONFLICT (object_id, property_name, model_version)
        DO UPDATE SET
            parent_id = EXCLUDED.parent_id,
            level = EXCLUDED.level,
            display_name = EXCLUDED.display_name,
            category = EXCLUDED.category,
            property_value = EXCLUDED.property_value,
            project_id = EXCLUDED.project_id,
            revision_id = EXCLUDED.revision_id,
            ifc_guid = COALESCE(EXCLUDED.ifc_guid, navisworks_hierarchy.ifc_guid),
            source_file_path = COALESCE(EXCLUDED.source_file_path, navisworks_hierarchy.source_file_path),
            source_system = EXCLUDED.source_system
        """,
        insert_rows,
    )

    async with db.transaction():
        inserted_count = 0
        skipped_count = 0

        for obj in aggregated_objects:
            try:
                parent_id = obj["parent_object_id"]
                if parent_id == "00000000-0000-0000-0000-000000000000":
                    parent_id = None

                element_id = obj["properties"].get("Element ID")
                if element_id:
                    try:
                        element_id = int(element_id)
                    except ValueError:
                        element_id = None

                ifc_guid = None
                for key in IFC_GUID_KEYS:
                    if key in obj["properties"]:
                        ifc_guid = obj["properties"][key]
                        break

                await db.execute(
                    """
                    INSERT INTO unified_objects (
                        project_id, revision_id, object_id, element_id,
                        source_type, parent_object_id, level, display_name,
                        category, properties, spatial_path, ifc_guid
                    )
                    VALUES (
                        $1, $2, $3, $4, 'navisworks', $5, $6, $7, $8, $9::jsonb, $10, $11
                    )
                    ON CONFLICT (revision_id, object_id, source_type) DO UPDATE
                    SET properties = unified_objects.properties || EXCLUDED.properties,
                        ifc_guid = COALESCE(EXCLUDED.ifc_guid, unified_objects.ifc_guid),
                        display_name = COALESCE(EXCLUDED.display_name, unified_objects.display_name),
                        category = COALESCE(EXCLUDED.category, unified_objects.category)
                    """,
                    project["id"],
                    revision_id,
                    obj["object_id"],
                    element_id,
                    parent_id,
                    obj["level"],
                    obj["display_name"],
                    obj["category"],
                    json.dumps(obj["properties"], ensure_ascii=False),
                    obj.get("display_name") or "",
                    ifc_guid,
                )
                inserted_count += 1
            except Exception as exc:  # pragma: no cover - logging only
                print(f"객체 삽입 실패 (ObjectId: {obj['object_id']}): {exc}")
                skipped_count += 1

        await db.execute(
            """
            UPDATE revisions
            SET
                navisworks_objects = (
                    SELECT COUNT(*) FROM unified_objects
                    WHERE revision_id = $1 AND source_type = 'navisworks'
                ),
                total_objects = (
                    SELECT COUNT(*) FROM unified_objects
                    WHERE revision_id = $1
                ),
                total_categories = (
                    SELECT COUNT(DISTINCT category) FROM unified_objects
                    WHERE revision_id = $1
                )
            WHERE id = $1
        """,
            revision_id,
        )

        if model_version:
            await db.execute(
                """
                INSERT INTO revision_versions (model_version, revision_id, source_type, source_file_path)
                VALUES ($1, $2, 'navisworks', $3)
                ON CONFLICT (model_version) DO UPDATE
                SET revision_id = EXCLUDED.revision_id,
                    source_type = CASE
                        WHEN revision_versions.source_type = 'revit' THEN 'both'
                        WHEN revision_versions.source_type = 'navisworks' THEN 'navisworks'
                        ELSE revision_versions.source_type
                    END,
                    source_file_path = COALESCE(EXCLUDED.source_file_path, revision_versions.source_file_path),
                    extracted_at = NOW()
                """,
                model_version,
                revision_id,
                source_path,
            )

    await update_spatial_paths(revision_id, db)

    response = {
        "message": "Navisworks 계층 데이터 업로드 완료",
        "project_code": project_code,
        "revision_number": revision_number,
        "inserted_count": inserted_count,
        "skipped_count": skipped_count,
        "total_objects": inserted_count,
    }
    if detection_result:
        response["detected"] = detection_result
    return response


async def update_spatial_paths(revision_id: Any, db):
    """Navisworks 계층 기반 spatial_path 갱신"""
    await db.execute(
        """
        WITH RECURSIVE hierarchy AS (
            SELECT
                id,
                object_id,
                parent_object_id,
                display_name,
                display_name::TEXT AS path
            FROM unified_objects
            WHERE revision_id = $1
              AND source_type = 'navisworks'
              AND parent_object_id IS NULL

            UNION ALL

            SELECT
                o.id,
                o.object_id,
                o.parent_object_id,
                o.display_name,
                h.path || ' > ' || o.display_name
            FROM unified_objects o
            JOIN hierarchy h ON o.parent_object_id = h.object_id
            WHERE o.revision_id = $1
              AND o.source_type = 'navisworks'
        )
        UPDATE unified_objects
        SET spatial_path = hierarchy.path
        FROM hierarchy
        WHERE unified_objects.id = hierarchy.id
          AND unified_objects.revision_id = $1
        """,
        revision_id,
    )


@router.get("/projects/{project_code}/hierarchy")
async def get_hierarchy_tree(
    project_code: str,
    revision_number: Optional[int] = None,
    max_level: int = 10,
    db=Depends(get_db),
):
    """
    Navisworks 계층 구조 조회 (트리 형태)
    """
    project = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code,
    )
    if not project:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    if revision_number:
        revision = await db.fetchrow(
            """
            SELECT id FROM revisions
            WHERE project_id = $1
              AND revision_number = $2
            """,
            project["id"],
            revision_number,
        )
    else:
        revision = await db.fetchrow(
            """
            SELECT id FROM revisions
            WHERE project_id = $1
              AND navisworks_objects > 0
            ORDER BY revision_number DESC
            LIMIT 1
            """,
            project["id"],
        )

    if not revision:
        raise HTTPException(404, "Navisworks 리비전을 찾을 수 없습니다")

    hierarchy = await db.fetch(
        """
        SELECT
            object_id,
            parent_object_id,
            level,
            display_name,
            category,
            spatial_path
        FROM unified_objects
        WHERE revision_id = $1
          AND source_type = 'navisworks'
          AND level <= $2
        ORDER BY level, display_name
        """,
        revision["id"],
        max_level,
    )

    return [dict(row) for row in hierarchy]
