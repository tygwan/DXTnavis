"""



프로젝트 관리 API 엔드포인트



"""



import hashlib



import json



import logging



import time



import uuid



from datetime import date, datetime



from typing import Any, Dict, List, Optional



from uuid import UUID







from fastapi import APIRouter, Depends, HTTPException



from pydantic import BaseModel, Field







from ..database import get_db



from ..models.schemas import DetectRequest, DetectResponse, DetectedProject



from ..utils.identifiers import generate_project_code, to_deterministic_uuid











router = APIRouter(prefix="/api/v1/projects", tags=["projects"])



logger = logging.getLogger(__name__)











# ============================================



# Response Caching (Phase B requirement)



# ============================================







# Simple in-memory cache: {cache_key: (response, timestamp)}



_DETECTION_CACHE: Dict[str, tuple[DetectResponse, float]] = {}



_CACHE_TTL_SECONDS = 300  # 5 minutes











def _get_cache_key(request: DetectRequest) -> str:



    """Generate deterministic cache key from request."""



    # Sort object_ids for consistent cache keys



    sorted_ids = sorted(request.object_ids)



    cache_data = {



        "object_ids": sorted_ids,



        "min_confidence": request.min_confidence,



        "max_candidates": request.max_candidates,



    }



    cache_str = json.dumps(cache_data, sort_keys=True)



    return hashlib.sha256(cache_str.encode()).hexdigest()











def _get_cached_response(cache_key: str) -> Optional[DetectResponse]:



    """Retrieve cached response if still valid."""



    if cache_key in _DETECTION_CACHE:



        cached_response, cached_time = _DETECTION_CACHE[cache_key]



        if time.time() - cached_time < _CACHE_TTL_SECONDS:



            return cached_response



        else:



            # Expired, remove from cache



            del _DETECTION_CACHE[cache_key]



    return None











def _cache_response(cache_key: str, response: DetectResponse) -> None:



    """Cache detection response with current timestamp."""



    _DETECTION_CACHE[cache_key] = (response, time.time())











def _normalize_metadata_value(value: Any) -> Any:



    """



    Convert metadata payload values into JSON-serialisable primitives.



    Handles common plugin payloads (datetime, sets, custom objects) defensively.



    """



    if value is None or isinstance(value, (str, int, float, bool)):



        return value



    if isinstance(value, (datetime, date)):



        return value.isoformat()



    if isinstance(value, dict):



        return {str(key): _normalize_metadata_value(val) for key, val in value.items()}



    if isinstance(value, (list, tuple, set)):



        return [_normalize_metadata_value(item) for item in value]



    return str(value)











def _sanitize_metadata(metadata: Optional[Dict[str, Any]]) -> Dict[str, Any]:



    """Ensure metadata is a dict containing JSON-safe primitives."""



    if not metadata:



        return {}



    return {str(key): _normalize_metadata_value(val) for key, val in metadata.items()}











# ============================================



# Pydantic Models (Legacy)



# ============================================







class ProjectCreate(BaseModel):



    """프로젝트 생성 요청"""



    code: Optional[str] = Field(None, description="프로젝트 코드 (자동 생성 가능)")



    name: str = Field(..., description="프로젝트 이름")



    revit_file_name: Optional[str] = None



    revit_file_path: Optional[str] = None



    project_number: Optional[str] = None



    client_name: Optional[str] = None



    address: Optional[str] = None



    building_name: Optional[str] = None



    latitude: Optional[float] = None



    longitude: Optional[float] = None



    elevation: Optional[float] = None



    timezone: Optional[int] = None



    created_by: str = Field(..., description="생성자")



    metadata: Optional[dict] = Field(default_factory=dict)











class ProjectResponse(BaseModel):



    """프로젝트 응답"""



    id: UUID



    code: str



    name: str



    revit_file_name: Optional[str] = None



    revit_file_path: Optional[str] = None



    project_number: Optional[str] = None



    client_name: Optional[str] = None



    address: Optional[str] = None



    building_name: Optional[str] = None



    latitude: Optional[float] = None



    longitude: Optional[float] = None



    elevation: Optional[float] = None



    timezone: Optional[int] = None



    created_by: str



    created_at: datetime



    updated_at: datetime



    is_active: bool



    metadata: dict







    class Config:



        from_attributes = True











class ProjectSummary(BaseModel):



    """프로젝트 요약"""



    code: str



    name: str



    client_name: Optional[str] = None



    latest_revit_revision: Optional[int] = None



    latest_navis_revision: Optional[int] = None



    last_updated: Optional[datetime] = None



    revit_objects_count: int = 0



    navisworks_objects_count: int = 0



    total_activities: int = 0



    overall_progress: Optional[float] = None















class ProjectDetectionRequest(BaseModel):



    """객체/IFC GUID 기반 프로젝트 감지 요청"""



    object_ids: List[str] = Field(



        default_factory=list,



        description="Navisworks/Revit에서 추출된 객체 식별자 목록",



    )



    ifc_guids: Optional[List[str]] = Field(



        default=None,



        description="객체의 IFC GUID 목록",



    )



    source_file_path: Optional[str] = Field(



        default=None,



        description="원본 모델 파일 경로 힌트 (선택)",



    )



    limit: int = Field(



        default=5,



        ge=1,



        le=20,



        description="검색 결과 최대 개수",



    )











class ProjectDetectionItem(BaseModel):



    """프로젝트 감지 결과 항목"""



    code: str



    name: str



    revision_id: UUID



    revision_number: Optional[int] = None



    source_type: str



    match_count: int



    total_objects: Optional[int] = None



    confidence: float











class ProjectDetectionResponse(BaseModel):



    """프로젝트 감지 API 응답 (legacy)"""



    success: bool



    detected_projects: List[ProjectDetectionItem]



    message: Optional[str] = None











# ============================================



# API Endpoints



# ============================================











@router.post("/detect-by-objects", response_model=DetectResponse)



async def detect_project_by_objects(



    request: DetectRequest,



    db=Depends(get_db),



):



    """



    Detect projects based on object IDs with Phase B dual-identity schema.







    Phase B requirements (techspec.md §3):



    - Only considers objects from the latest revision for each project



    - Matches by unique_key OR object_guid



    - Implements response caching with TTL=300s



    - Returns confidence (matched/total_in_latest) and coverage (matched/input) metrics



    """



    # Check cache first



    cache_key = _get_cache_key(request)



    cached = _get_cached_response(cache_key)



    if cached:



        logger.debug("Detection cache hit: %s", cache_key[:16])



        return cached







    # Query only from latest revisions using v_unified_objects_latest view



    # Match by unique_key OR object_guid



    query = """



        WITH latest_project_stats AS (



            -- Get total object count per project in latest revision



            SELECT



                r.project_id,



                COUNT(*) AS total_objects_latest



            FROM v_unified_objects_latest uo



            JOIN revisions r ON r.id = uo.revision_id



            GROUP BY r.project_id



        ),



        matched_objects AS (



            -- Find matching objects in latest revisions



            SELECT DISTINCT



                r.project_id,



                r.id AS revision_id,



                uo.unique_key



            FROM v_unified_objects_latest uo



            JOIN revisions r ON r.id = uo.revision_id



            WHERE uo.unique_key = ANY($1::text[])



               OR uo.object_guid::text = ANY($1::text[])



        )



        SELECT



            p.id::text AS project_id,



            p.code,



            p.name,



            lps.total_objects_latest,



            COUNT(DISTINCT mo.unique_key) AS match_count



        FROM matched_objects mo



        JOIN projects p ON p.id = mo.project_id



        JOIN latest_project_stats lps ON lps.project_id = p.id



        GROUP BY p.id, p.code, p.name, lps.total_objects_latest



        HAVING (COUNT(DISTINCT mo.unique_key)::float / lps.total_objects_latest) >= $2



        ORDER BY COUNT(DISTINCT mo.unique_key) DESC



        LIMIT $3



    """







    rows = await db.fetch(



        query,



        (request.object_ids, request.min_confidence, request.max_candidates)



    )







    detected_projects = []



    input_count = len(request.object_ids)







    for row in rows:



        match_count = row["match_count"]



        total_objects = row["total_objects_latest"]



        confidence = match_count / total_objects if total_objects > 0 else 0.0



        coverage = match_count / input_count if input_count > 0 else 0.0







        detected_projects.append(



            DetectedProject(



                id=row["project_id"],



                code=row["code"],



                name=row["name"],



                confidence=round(confidence, 4),



                match_count=match_count,



                total_objects=total_objects,



                coverage=round(coverage, 4),



            )



        )







    response = DetectResponse(



        success=len(detected_projects) > 0,



        detected_projects=detected_projects,



        query_object_count=input_count,



        message=f"Detected {len(detected_projects)} project(s)" if detected_projects else "No matching projects found"



    )







    # Cache the response



    _cache_response(cache_key, response)







    logger.info(



        "Phase B detection: input=%d matches=%d cached=%s",



        input_count,



        len(detected_projects),



        cache_key[:16]



    )







    return response











@router.post("/detect-by-objects/legacy", response_model=ProjectDetectionResponse)



async def detect_project_by_objects_legacy(



    payload: ProjectDetectionRequest,



    db=Depends(get_db),



):



    """



    Legacy project detection endpoint (backward compatibility).







    Navisworks/Revit 객체 식별자 또는 IFC GUID 기반으로 기존 프로젝트를 탐색한다.



    """



    normalized_object_ids: List[uuid.UUID] = []



    for raw in payload.object_ids:



        if not raw:



            continue



        try:



            normalized_object_ids.append(to_deterministic_uuid(raw))



        except ValueError:



            logger.debug("Skipping invalid object identifier: %s", raw)







    ifc_guids = [



        guid.strip()



        for guid in (payload.ifc_guids or [])



        if guid and guid.strip()



    ]



    source_hint = payload.source_file_path.strip() if payload.source_file_path else None



    if not source_hint:



        source_hint = None







    if not normalized_object_ids and not ifc_guids and not source_hint:



        raise HTTPException(



            status_code=400,



            detail="At least one of objectIds, ifcGuids, or sourceFilePath is required.",



        )







    conditions: List[str] = []



    params: List[Any] = []







    if normalized_object_ids:



        params.append(normalized_object_ids)



        conditions.append(f"uo.object_id = ANY(${len(params)}::uuid[])")



    if ifc_guids:



        params.append(ifc_guids)



        conditions.append(f"uo.ifc_guid = ANY(${len(params)}::text[])")



    if source_hint:



        params.append(source_hint)



        placeholder = len(params)



        conditions.append(



            f"(r.source_file_path = ${placeholder} OR rv.source_file_path = ${placeholder})"



        )







    where_clause = " OR ".join(f"({clause})" for clause in conditions)







    limit = min(payload.limit, 20)



    params.append(limit)







    query = f"""



        SELECT



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



        GROUP BY p.code, p.name, r.id, r.revision_number, r.source_type, r.total_objects



        ORDER BY COUNT(DISTINCT uo.object_id) DESC, r.revision_number DESC NULLS LAST



        LIMIT ${len(params)}



    """







    rows = await db.fetch(query, tuple(params))







    detected: List[ProjectDetectionItem] = []



    for row in rows:



        total_objects = row["total_objects"] or 0



        match_count = row["match_count"]



        denominator = total_objects if total_objects else match_count



        confidence = float(match_count / denominator) if denominator else 0.0



        detected.append(



            ProjectDetectionItem(



                code=row["code"],



                name=row["name"],



                revision_id=row["revision_id"],



                revision_number=row["revision_number"],



                source_type=row["source_type"],



                match_count=match_count,



                total_objects=total_objects or None,



                confidence=round(confidence, 4),



            )



        )







    message = (



        f"Detected {len(detected)} project(s)."



        if detected



        else "No matching project found."



    )







    logger.info(



        "Legacy detection request: objects=%d ifc=%d source_hint=%s -> matches=%d",



        len(normalized_object_ids),



        len(ifc_guids),



        bool(source_hint),



        len(detected),



    )







    return ProjectDetectionResponse(



        success=bool(detected),



        detected_projects=detected,



        message=message,



    )











@router.get("/{project_code}", response_model=ProjectResponse)
async def get_project(project_code: str, db = Depends(get_db)):
    """
    프로젝트 조회

    - **project_code**: 프로젝트 코드
    """
    result = await db.fetchrow(
        "SELECT * FROM projects WHERE code = $1",
        project_code
    )
    if not result:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    return dict(result)

@router.post("", response_model=ProjectResponse, status_code=201)
async def create_project(project: ProjectCreate, db = Depends(get_db)):
    """
    프로젝트 생성

    - **code**: 프로젝트 코드 (미제공시 name에서 자동 생성)
    - **name**: 프로젝트 이름 (필수)
    - **created_by**: 생성자 (필수)
    """
    # 1. 프로젝트 코드 생성 또는 검증
    if not project.code:
        project.code = generate_project_code(project.name)

    # 2. 중복 확인
    existing = await db.fetchval(
        "SELECT id FROM projects WHERE code = $1",
        project.code
    )
    if existing:
        raise HTTPException(409, f"Project code '{project.code}' already exists.")

    # 3. 프로젝트 생성
    sanitized_metadata = _sanitize_metadata(project.metadata)
    project_id = await db.fetchval(
        """
        INSERT INTO projects (
            code, name, revit_file_name, revit_file_path,
            project_number, client_name, address, building_name,
            latitude, longitude, elevation, timezone,
            created_by, metadata
        ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14::jsonb)
        RETURNING id
        """,
        project.code,
        project.name,
        project.revit_file_name,
        project.revit_file_path,
        project.project_number,
        project.client_name,
        project.address,
        project.building_name,
        project.latitude,
        project.longitude,
        project.elevation,
        project.timezone,
        project.created_by,
        json.dumps(sanitized_metadata, ensure_ascii=False),
    )

    # 4. 생성된 프로젝트 반환
    result = await db.fetchrow("SELECT * FROM projects WHERE id = $1", project_id)
    return dict(result)

@router.patch("/{project_code}", response_model=ProjectResponse)
async def update_project(
    project_code: str,
    update_data: dict,
    db = Depends(get_db)
):
    """
    ?�로?�트 ?�보 ?�데?�트

    - **project_code**: ?�로?�트 코드
    - **update_data**: ?�데?�트???�드??
    """
    # 1. 프로젝트 존재 확인
    project = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code
    )
    if not project:
        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")

    # 2. 업데이트 가능한 필드만 추출
    allowed_fields = {
        'name', 'revit_file_name', 'revit_file_path',
        'project_number', 'client_name', 'address', 'building_name',
        'latitude', 'longitude', 'elevation', 'timezone', 'metadata'
    }

    filtered_updates = {k: v for k, v in update_data.items() if k in allowed_fields}
    if not filtered_updates:
        raise HTTPException(400, "업데이트할 필드가 없습니다")

    # 3. 동적 UPDATE 쿼리 생성
    set_clauses: List[str] = []
    param_values: List[Any] = []
    for field, value in filtered_updates.items():
        if field == "metadata":
            set_clauses.append(f"{field} = ${len(param_values) + 2}::jsonb")
            param_values.append(json.dumps(_sanitize_metadata(value), ensure_ascii=False))
        else:
            set_clauses.append(f"{field} = ${len(param_values) + 2}")
            param_values.append(value)

    query = f"""
        UPDATE projects
        SET {', '.join(set_clauses)}, updated_at = NOW()
        WHERE code = $1
        RETURNING *
    """

    updated_project = await db.fetchrow(
        query,
        project_code,
        *param_values
    )
    return dict(updated_project)

@router.delete("/{project_code}")



async def delete_project(



    project_code: str,



    hard_delete: bool = False,



    db = Depends(get_db)



):



    """



    프로젝트 삭제







    - **project_code**: 프로젝트 코드



    - **hard_delete**: true면 물리 삭제, false면 논리 삭제 (기본: false)



    """



    if hard_delete:



        # 물리 삭제 (CASCADE로 관련 데이터 모두 삭제)



        deleted = await db.execute(



            "DELETE FROM projects WHERE code = $1",



            project_code



        )



    else:



        # 논리 삭제 (is_active = false)



        deleted = await db.execute(



            "UPDATE unified_projects SET is_active = false, updated_at = NOW() WHERE code = $1",



            project_code



        )







    if deleted == "DELETE 0" or deleted == "UPDATE 0":



        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")







    return {"message": f"프로젝트 '{project_code}' 삭제 완료"}











@router.get("/{project_code}/stats")



async def get_project_stats(project_code: str, db = Depends(get_db)):



    """



    프로젝트 통계 정보







    - **project_code**: 프로젝트 코드



    """



    # 1. 프로젝트 존재 확인



    project = await db.fetchrow(



        "SELECT id FROM projects WHERE code = $1",



        project_code



    )



    if not project:



        raise HTTPException(404, f"프로젝트 '{project_code}'를 찾을 수 없습니다")







    project_id = project['id']







    # 2. 통계 쿼리



    stats = await db.fetchrow("""



        SELECT



            (SELECT COUNT(*) FROM revisions WHERE project_id = $1) AS total_revisions,



            (SELECT COUNT(*) FROM revisions WHERE project_id = $1 AND source_type = 'revit') AS revit_revisions,



            (SELECT COUNT(*) FROM revisions WHERE project_id = $1 AND source_type = 'navisworks') AS navis_revisions,



            (SELECT COUNT(*) FROM unified_objects o



             JOIN revisions r ON o.revision_id = r.id



             WHERE r.project_id = $1) AS total_objects,



            (SELECT COUNT(DISTINCT category) FROM unified_objects o



             JOIN revisions r ON o.revision_id = r.id



             WHERE r.project_id = $1) AS total_categories,



            (SELECT COUNT(*) FROM activities WHERE project_id = $1) AS total_activities



    """, project_id)







    return {



        "project_code": project_code,



        **dict(stats)



    }
