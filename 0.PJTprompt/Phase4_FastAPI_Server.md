# Phase 4: FastAPI 서버 개발

## 문서 목적
FastAPI 기반 백엔드 서버의 구현 가이드입니다. 보안 게이트웨이, 데이터 검증, API 엔드포인트 구현을 다룹니다.

---

## 1. 개요

### 1.1. FastAPI 서버의 역할
**핵심 책임**: 보안 게이트웨이 및 데이터 중개자

**주요 기능**:
1. 데이터베이스와 클라이언트 간 유일한 통신 창구
2. 데이터 검증 및 보안
3. RESTful API 제공
4. 인증/인가 관리
5. 비즈니스 로직 실행

### 1.2. 기술 스펙
- **프레임워크**: FastAPI 0.100+
- **Python 버전**: 3.11+
- **DB 드라이버**: psycopg2 또는 asyncpg
- **배포**: Uvicorn + Gunicorn

---

## 2. 프로젝트 구조

### 2.1. 폴더 구조
```
fastapi_server/
├── main.py                          # FastAPI 애플리케이션 진입점
├── config.py                        # 설정 관리
├── database.py                      # DB 연결 관리
├── requirements.txt                 # Python 의존성
├── .env                            # 환경 변수 (gitignore)
├── models/
│   ├── __init__.py
│   ├── schemas.py                   # Pydantic 스키마
│   └── orm_models.py                # SQLAlchemy ORM 모델 (선택사항)
├── routers/
│   ├── __init__.py
│   ├── ingest.py                    # 데이터 수집 엔드포인트
│   ├── analytics.py                 # 분석 데이터 엔드포인트
│   └── timeliner.py                 # 4D 연동 엔드포인트
├── services/
│   ├── __init__.py
│   ├── validation.py                # 데이터 검증 서비스
│   ├── transformation.py            # 데이터 가공 서비스
│   └── database_service.py          # DB 작업 서비스
├── middleware/
│   ├── __init__.py
│   ├── logging_middleware.py        # 로깅 미들웨어
│   └── cors_middleware.py           # CORS 미들웨어
└── tests/
    ├── __init__.py
    ├── test_ingest.py
    └── test_analytics.py
```

---

## 3. 의존성 및 설정

### 3.1. requirements.txt

```txt
fastapi==0.100.0
uvicorn[standard]==0.22.0
psycopg2-binary==2.9.6
pydantic==2.0.0
pydantic-settings==2.0.0
python-dotenv==1.0.0
python-multipart==0.0.6
```

### 3.2. .env (환경 변수)

```env
# 데이터베이스 설정
DATABASE_URL=postgresql://dx_api_role:strong_password@localhost:5432/dx_platform

# 서버 설정
HOST=0.0.0.0
PORT=5000
DEBUG=True

# 보안 설정
SECRET_KEY=your-secret-key-here
ALLOWED_ORIGINS=http://localhost,http://127.0.0.1

# 로깅 설정
LOG_LEVEL=INFO
LOG_FILE=logs/dx_api.log
```

**주의사항**:
- ❌ .env 파일은 절대 Git에 커밋하지 말 것
- ✅ .env.example 파일을 만들어 템플릿 제공
- ⚠️ 운영 환경에서는 환경 변수로 관리

### 3.3. config.py (설정 관리)

```python
from pydantic_settings import BaseSettings
from typing import List

class Settings(BaseSettings):
    """애플리케이션 설정"""

    # 데이터베이스
    DATABASE_URL: str

    # 서버
    HOST: str = "0.0.0.0"
    PORT: int = 5000
    DEBUG: bool = False

    # 보안
    SECRET_KEY: str
    ALLOWED_ORIGINS: List[str] = ["*"]

    # 로깅
    LOG_LEVEL: str = "INFO"
    LOG_FILE: str = "logs/dx_api.log"

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"

# 싱글톤 인스턴스
settings = Settings()
```

---

## 4. 데이터베이스 연결

### 4.1. database.py

```python
import psycopg2
from psycopg2.extras import RealDictCursor
from contextlib import contextmanager
from config import settings
import logging

logger = logging.getLogger(__name__)

class Database:
    """데이터베이스 연결 관리 클래스"""

    def __init__(self):
        self.connection_string = settings.DATABASE_URL

    @contextmanager
    def get_connection(self):
        """컨텍스트 매니저로 DB 연결 제공"""
        conn = None
        try:
            conn = psycopg2.connect(
                self.connection_string,
                cursor_factory=RealDictCursor
            )
            yield conn
            conn.commit()
        except Exception as e:
            if conn:
                conn.rollback()
            logger.error(f"데이터베이스 오류: {e}")
            raise
        finally:
            if conn:
                conn.close()

    def execute_query(self, query: str, params: tuple = None):
        """쿼리 실행 (SELECT)"""
        with self.get_connection() as conn:
            with conn.cursor() as cursor:
                cursor.execute(query, params)
                return cursor.fetchall()

    def execute_many(self, query: str, data_list: list):
        """배치 INSERT"""
        with self.get_connection() as conn:
            with conn.cursor() as cursor:
                cursor.executemany(query, data_list)
                return cursor.rowcount

# 싱글톤 인스턴스
db = Database()
```

---

## 5. Pydantic 스키마 정의

### 5.1. models/schemas.py

```python
from pydantic import BaseModel, Field, validator
from typing import List, Optional, Dict, Any
from datetime import datetime

class MetadataSchema(BaseModel):
    """메타데이터 스키마"""
    model_version: str = Field(..., min_length=1, max_length=255)
    timestamp: datetime
    project_name: str = Field(..., min_length=1, max_length=255)
    created_by: str = Field(..., min_length=1, max_length=100)
    description: Optional[str] = None
    total_object_count: int = Field(default=0, ge=0)
    revit_file_path: Optional[str] = None

    @validator('model_version')
    def validate_model_version(cls, v):
        """ModelVersion 형식 검증"""
        if not v.replace('-', '').replace('_', '').isalnum():
            raise ValueError('ModelVersion은 영문, 숫자, -, _ 만 허용됩니다.')
        return v

class ObjectSchema(BaseModel):
    """객체 스키마"""
    model_version: str
    object_id: str = Field(..., min_length=1, max_length=255)
    element_id: int
    category: str = Field(..., min_length=1, max_length=255)
    family: Optional[str] = None
    type: Optional[str] = None
    activity_id: Optional[str] = Field(None, max_length=100)
    properties: Optional[Dict[str, Any]] = None
    bounding_box: Optional[Dict[str, float]] = None

class RelationshipSchema(BaseModel):
    """관계 스키마"""
    model_version: str
    source_object_id: str = Field(..., min_length=1, max_length=255)
    target_object_id: str = Field(..., min_length=1, max_length=255)
    relation_type: str = Field(..., min_length=1, max_length=50)
    is_directed: bool = True

class ExtractedDataSchema(BaseModel):
    """추출된 전체 데이터 스키마"""
    Metadata: MetadataSchema
    Objects: List[ObjectSchema]
    Relationships: List[RelationshipSchema]

class ApiResponse(BaseModel):
    """API 응답 스키마"""
    success: bool
    message: str
    data: Optional[Any] = None
    timestamp: datetime = Field(default_factory=datetime.utcnow)

class VersionSummary(BaseModel):
    """버전 요약 스키마"""
    model_version: str
    timestamp: datetime
    project_name: str
    created_by: str
    description: Optional[str]
    total_object_count: int
    category_breakdown: Dict[str, int]
    linkable_object_count: int
    total_relationship_count: int

class ObjectDelta(BaseModel):
    """객체 변경 스키마"""
    object_id: str
    change_type: str  # ADDED, DELETED, MODIFIED
    category: str
    family: Optional[str]
    type: Optional[str]
    activity_id: Optional[str]
    properties_v1: Optional[Dict[str, Any]]
    properties_v2: Optional[Dict[str, Any]]

class TimelinerMapping(BaseModel):
    """TimeLiner 매핑 스키마"""
    model_version: str
    activity_id: str
    object_id: str
    element_id: int
    category: str
    family: Optional[str]
    type: Optional[str]
    properties: Optional[Dict[str, Any]]
```

---

## 6. API 엔드포인트 구현

### 6.1. routers/ingest.py (데이터 수집)

```python
from fastapi import APIRouter, HTTPException, status
from models.schemas import ExtractedDataSchema, ApiResponse
from services.validation import validate_extracted_data
from services.database_service import save_extracted_data
import logging

router = APIRouter(prefix="/api/v1", tags=["ingest"])
logger = logging.getLogger(__name__)

@router.post("/ingest", response_model=ApiResponse, status_code=status.HTTP_201_CREATED)
async def ingest_data(data: ExtractedDataSchema):
    """
    DXrevit으로부터 추출된 BIM 데이터를 수신하여 데이터베이스에 저장

    - **data**: 메타데이터, 객체, 관계를 포함한 전체 추출 데이터

    Returns:
    - 성공 시: 201 Created
    - 실패 시: 400 Bad Request 또는 500 Internal Server Error
    """
    try:
        logger.info(f"데이터 수집 요청: ModelVersion={data.Metadata.model_version}")

        # 1. 데이터 검증
        validation_result = validate_extracted_data(data)
        if not validation_result["valid"]:
            logger.warning(f"데이터 검증 실패: {validation_result['errors']}")
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail={"errors": validation_result["errors"]}
            )

        # 2. 데이터베이스에 저장
        saved_counts = save_extracted_data(data)

        logger.info(f"데이터 저장 완료: {saved_counts}")

        return ApiResponse(
            success=True,
            message="데이터가 성공적으로 저장되었습니다.",
            data=saved_counts
        )

    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"데이터 수집 중 오류: {e}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"서버 오류: {str(e)}"
        )
```

### 6.2. routers/analytics.py (분석 데이터)

```python
from fastapi import APIRouter, HTTPException, Query, status
from models.schemas import ApiResponse, VersionSummary, ObjectDelta
from services.database_service import (
    get_all_versions,
    get_version_summary,
    compare_versions
)
from typing import List
import logging

router = APIRouter(prefix="/api/v1", tags=["analytics"])
logger = logging.getLogger(__name__)

@router.get("/models/versions", response_model=List[str])
async def list_versions(
    project_name: str = Query(None, description="프로젝트 이름으로 필터링")
):
    """
    모든 버전 목록 조회

    - **project_name**: (선택사항) 특정 프로젝트의 버전만 조회

    Returns:
    - 버전 목록 (타임스탬프 내림차순)
    """
    try:
        versions = get_all_versions(project_name)
        return versions
    except Exception as e:
        logger.error(f"버전 목록 조회 오류: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=str(e)
        )

@router.get("/models/{version}/summary", response_model=VersionSummary)
async def get_summary(version: str):
    """
    특정 버전의 요약 정보 조회

    - **version**: 모델 버전

    Returns:
    - 버전 요약 정보 (객체 수, 카테고리별 분석 등)
    """
    try:
        summary = get_version_summary(version)
        if not summary:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail=f"버전 '{version}'을 찾을 수 없습니다."
            )
        return summary
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"버전 요약 조회 오류: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=str(e)
        )

@router.get("/models/compare", response_model=List[ObjectDelta])
async def compare_model_versions(
    v1: str = Query(..., description="비교할 첫 번째 버전"),
    v2: str = Query(..., description="비교할 두 번째 버전"),
    change_type: str = Query(None, regex="^(ADDED|DELETED|MODIFIED)$", description="변경 유형 필터")
):
    """
    두 버전 간 변경 사항 비교

    - **v1**: 첫 번째 버전 (이전 버전)
    - **v2**: 두 번째 버전 (최신 버전)
    - **change_type**: (선택사항) 변경 유형 필터 (ADDED, DELETED, MODIFIED)

    Returns:
    - 변경된 객체 목록
    """
    try:
        deltas = compare_versions(v1, v2, change_type)
        return deltas
    except Exception as e:
        logger.error(f"버전 비교 오류: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=str(e)
        )
```

### 6.3. routers/timeliner.py (4D 연동)

```python
from fastapi import APIRouter, HTTPException, status
from models.schemas import TimelinerMapping
from services.database_service import get_timeliner_mapping
from typing import List
import logging

router = APIRouter(prefix="/api/v1", tags=["timeliner"])
logger = logging.getLogger(__name__)

@router.get("/timeliner/{version}/mapping", response_model=List[TimelinerMapping])
async def get_timeliner_link_data(version: str):
    """
    TimeLiner 자동 연결을 위한 매핑 데이터 조회

    - **version**: 모델 버전

    Returns:
    - ActivityId와 ObjectId 매핑 목록
    """
    try:
        logger.info(f"TimeLiner 매핑 데이터 요청: {version}")

        mappings = get_timeliner_mapping(version)

        if not mappings:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail=f"버전 '{version}'의 매핑 데이터를 찾을 수 없습니다."
            )

        logger.info(f"매핑 데이터 반환: {len(mappings)}개")
        return mappings

    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"TimeLiner 매핑 조회 오류: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=str(e)
        )
```

---

## 7. 서비스 레이어 구현

### 7.1. services/validation.py

```python
from models.schemas import ExtractedDataSchema
from typing import Dict, List

def validate_extracted_data(data: ExtractedDataSchema) -> Dict[str, any]:
    """
    추출된 데이터의 유효성 검증

    Returns:
    - {"valid": True/False, "errors": [...]}
    """
    errors = []

    # 1. 메타데이터 검증
    if not data.Metadata.model_version:
        errors.append("ModelVersion은 필수 항목입니다.")

    if not data.Metadata.project_name:
        errors.append("ProjectName은 필수 항목입니다.")

    if not data.Metadata.created_by:
        errors.append("CreatedBy는 필수 항목입니다.")

    # 2. 객체 수 일관성 검증
    if data.Metadata.total_object_count != len(data.Objects):
        errors.append(f"객체 수 불일치: 메타데이터={data.Metadata.total_object_count}, 실제={len(data.Objects)}")

    # 3. 객체 ID 중복 검증
    object_ids = [obj.object_id for obj in data.Objects]
    if len(object_ids) != len(set(object_ids)):
        errors.append("중복된 ObjectId가 존재합니다.")

    # 4. 관계 유효성 검증 (source/target이 객체 목록에 존재하는지)
    object_id_set = set(object_ids)
    for rel in data.Relationships:
        if rel.source_object_id not in object_id_set:
            errors.append(f"관계의 SourceObjectId '{rel.source_object_id}'가 객체 목록에 없습니다.")
        if rel.target_object_id not in object_id_set:
            errors.append(f"관계의 TargetObjectId '{rel.target_object_id}'가 객체 목록에 없습니다.")

    return {
        "valid": len(errors) == 0,
        "errors": errors
    }
```

### 7.2. services/database_service.py

```python
from database import db
from models.schemas import ExtractedDataSchema
import json
import logging

logger = logging.getLogger(__name__)

def save_extracted_data(data: ExtractedDataSchema) -> dict:
    """추출된 데이터를 데이터베이스에 저장"""

    # 1. 메타데이터 저장
    metadata_query = """
        INSERT INTO metadata (
            model_version, timestamp, project_name, created_by,
            description, total_object_count, revit_file_path
        ) VALUES (%s, %s, %s, %s, %s, %s, %s)
    """
    metadata_params = (
        data.Metadata.model_version,
        data.Metadata.timestamp,
        data.Metadata.project_name,
        data.Metadata.created_by,
        data.Metadata.description,
        data.Metadata.total_object_count,
        data.Metadata.revit_file_path
    )

    db.execute_query(metadata_query, metadata_params)
    logger.info(f"메타데이터 저장 완료: {data.Metadata.model_version}")

    # 2. 객체 저장 (배치)
    objects_query = """
        INSERT INTO objects (
            model_version, object_id, element_id, category, family, type,
            activity_id, properties, bounding_box
        ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
    """
    objects_data = [
        (
            obj.model_version,
            obj.object_id,
            obj.element_id,
            obj.category,
            obj.family,
            obj.type,
            obj.activity_id,
            json.dumps(obj.properties) if obj.properties else None,
            json.dumps(obj.bounding_box) if obj.bounding_box else None
        )
        for obj in data.Objects
    ]

    objects_count = db.execute_many(objects_query, objects_data)
    logger.info(f"객체 저장 완료: {objects_count}개")

    # 3. 관계 저장 (배치)
    if data.Relationships:
        relationships_query = """
            INSERT INTO relationships (
                model_version, source_object_id, target_object_id, relation_type, is_directed
            ) VALUES (%s, %s, %s, %s, %s)
        """
        relationships_data = [
            (
                rel.model_version,
                rel.source_object_id,
                rel.target_object_id,
                rel.relation_type,
                rel.is_directed
            )
            for rel in data.Relationships
        ]

        relationships_count = db.execute_many(relationships_query, relationships_data)
        logger.info(f"관계 저장 완료: {relationships_count}개")
    else:
        relationships_count = 0

    return {
        "metadata_count": 1,
        "objects_count": objects_count,
        "relationships_count": relationships_count
    }

def get_all_versions(project_name: str = None) -> list:
    """모든 버전 목록 조회"""
    if project_name:
        query = "SELECT model_version FROM metadata WHERE project_name = %s ORDER BY timestamp DESC"
        results = db.execute_query(query, (project_name,))
    else:
        query = "SELECT model_version FROM metadata ORDER BY timestamp DESC"
        results = db.execute_query(query)

    return [row['model_version'] for row in results]

def get_version_summary(version: str) -> dict:
    """버전 요약 정보 조회"""
    query = "SELECT * FROM analytics_version_summary WHERE model_version = %s"
    results = db.execute_query(query, (version,))

    if results:
        return dict(results[0])
    return None

def compare_versions(v1: str, v2: str, change_type: str = None) -> list:
    """두 버전 간 변경 사항 비교"""
    query = "SELECT * FROM fn_compare_versions(%s, %s)"

    if change_type:
        query += " WHERE change_type = %s"
        results = db.execute_query(query, (v1, v2, change_type))
    else:
        results = db.execute_query(query, (v1, v2))

    return [dict(row) for row in results]

def get_timeliner_mapping(version: str) -> list:
    """TimeLiner 매핑 데이터 조회"""
    query = "SELECT * FROM analytics_4d_link_data WHERE model_version = %s ORDER BY activity_id"
    results = db.execute_query(query, (version,))

    return [dict(row) for row in results]
```

---

## 8. 메인 애플리케이션

### 8.1. main.py

```python
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from contextlib import asynccontextmanager
import logging
import uvicorn

from config import settings
from routers import ingest, analytics, timeliner

# 로깅 설정
logging.basicConfig(
    level=settings.LOG_LEVEL,
    format='[%(asctime)s] [%(levelname)s] [%(name)s] %(message)s',
    handlers=[
        logging.FileHandler(settings.LOG_FILE),
        logging.StreamHandler()
    ]
)

logger = logging.getLogger(__name__)

@asynccontextmanager
async def lifespan(app: FastAPI):
    """애플리케이션 생명주기 관리"""
    logger.info("DX Platform API 서버 시작")
    yield
    logger.info("DX Platform API 서버 종료")

# FastAPI 앱 생성
app = FastAPI(
    title="DX Platform API",
    description="BIM 데이터 통합 플랫폼 API",
    version="1.0.0",
    lifespan=lifespan
)

# CORS 미들웨어
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.ALLOWED_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 라우터 등록
app.include_router(ingest.router)
app.include_router(analytics.router)
app.include_router(timeliner.router)

@app.get("/")
async def root():
    """헬스 체크"""
    return {
        "service": "DX Platform API",
        "status": "running",
        "version": "1.0.0"
    }

if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=settings.DEBUG
    )
```

---

## 9. 실행 및 배포

### 9.1. 로컬 개발 환경 실행

```bash
# 가상 환경 생성
python -m venv venv

# 가상 환경 활성화 (Linux/Mac)
source venv/bin/activate

# 가상 환경 활성화 (Windows)
venv\Scripts\activate

# 의존성 설치
pip install -r requirements.txt

# 서버 실행 (개발 모드)
python main.py
```

### 9.2. 프로덕션 배포 (Gunicorn + Uvicorn)

```bash
# Gunicorn 설치
pip install gunicorn

# 프로덕션 실행
gunicorn main:app \
    --workers 4 \
    --worker-class uvicorn.workers.UvicornWorker \
    --bind 0.0.0.0:5000 \
    --log-level info
```

---

## 10. 주의사항 및 금지사항

### 10.1. ✅ 해야 할 것

**보안**:
- ✅ 환경 변수로 민감 정보 관리
- ✅ 입력 데이터 검증 (Pydantic)
- ✅ SQL 인젝션 방지 (파라미터화된 쿼리)
- ✅ HTTPS 사용 (프로덕션)

**에러 처리**:
- ✅ 의미 있는 HTTP 상태 코드 사용
- ✅ 자세한 로깅
- ✅ 사용자 친화적 오류 메시지

### 10.2. ❌ 하지 말아야 할 것

**보안**:
- ❌ 민감 정보 소스코드에 하드코딩
- ❌ 오류 메시지에 민감 정보 노출
- ❌ SQL 쿼리에 사용자 입력 직접 삽입

**API 설계**:
- ❌ UPDATE/DELETE 엔드포인트 제공
- ❌ 인증 없이 민감 데이터 노출
- ❌ 페이지네이션 없는 대용량 데이터 반환

---

## 11. 다음 단계

FastAPI 서버 구현 완료 후:
1. Phase 5 (DXnavis 개발) 참조
2. Phase 6 (통합 테스트 및 배포) 참조
3. Postman으로 API 테스트
