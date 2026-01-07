# 데이터 통합 전략 및 구현 가이드

**작성일**: 2025-10-18
**프로젝트**: AWP 2025 BIM Data Integration System
**목적**: Revit + Navisworks 데이터 통합 및 BI 파이프라인 구축

---

## 📋 목차

1. [현재 상황 분석](#현재-상황-분석)
2. [질문 1: Revit + Navisworks 동일 프로젝트 데이터 통합](#질문-1-revit--navisworks-동일-프로젝트-데이터-통합)
3. [질문 2: 프로젝트명 기반 스키마 및 리비전 관리](#질문-2-프로젝트명-기반-스키마-및-리비전-관리)
4. [질문 3: Navisworks 계층 데이터 업로드](#질문-3-navisworks-계층-데이터-업로드)
5. [질문 4: 메타데이터 구조 및 데이터 파이프라인](#질문-4-메타데이터-구조-및-데이터-파이프라인)
6. [통합 실행 계획](#통합-실행-계획)

---

## 🔍 현재 상황 분석

### Navisworks CSV 데이터 분석 결과

**파일**: `navis_Hierarchy_20251018_205342.csv`
**총 레코드**: 4,317개 (헤더 제외)

#### 데이터 구조 특징

```csv
ObjectId,ParentId,Level,DisplayName,Category,PropertyName,PropertyValue
00000000-0000-0000-0000-000000000000,00000000-0000-0000-0000-000000000000,0,배관테스트_4D.nwc,항목,이름,DisplayString:배관테스트_4D.nwc
00000000-0000-0000-0000-000000000000,00000000-0000-0000-0000-000000000000,0,배관테스트_4D.nwc,항목,소스 파일 이름,DisplayString:C:\Users\...\배관테스트.rvt
```

**핵심 발견사항**:

1. ✅ **계층 구조 완벽 지원**
   - `ObjectId`: 객체 고유 ID (GUID)
   - `ParentId`: 부모 객체 ID (계층 구조)
   - `Level`: 계층 깊이 (0부터 시작)
   - `DisplayName`: 표시 이름

2. ✅ **프로젝트 식별 정보**
   - 파일명: `배관테스트_4D.nwc`
   - 소스 파일: `배관테스트.rvt` ← **Revit 파일명과 일치!**
   - Project Name: `프로젝트 이름`

3. ✅ **EAV 패턴 (Entity-Attribute-Value)**
   - 각 속성마다 별도 행
   - 유연한 스키마 (속성 동적 추가 가능)

4. ⚠️ **문제점**
   - 현재 SQL로 전송 안됨 (CSV만 저장)
   - Revit 데이터와 연결 메커니즘 없음

### SQL 현재 데이터 분석

**프로젝트 이름** (Revit):
```sql
SELECT * FROM metadata WHERE project_name = '프로젝트 이름';
-- model_version: 프로젝트 이름_20251016_030006
-- total_object_count: 852
```

**배관테스트** (Navisworks):
- CSV 파일만 존재, SQL에 없음
- 소스 파일: `배관테스트.rvt`

---

## 질문 1: Revit + Navisworks 동일 프로젝트 데이터 통합

### 문제 정의

**현재 상황**:
```
Revit 플러그인 (DXrevit)
  ↓
  배관테스트.rvt 열기
  ↓
  스냅샷 → SQL (objects 테이블)
  - 평면 구조 (parent_id ❌)
  - Element ID, Category, Properties

Navisworks 플러그인 (DXnavis)
  ↓
  배관테스트.rvt → 배관테스트_4D.nwc 변환
  ↓
  계층 추출 → CSV 저장 (SQL ❌)
  - 계층 구조 (parent_id ✅)
  - ObjectId, Level, DisplayName
```

**목표**: **같은 Revit 파일에서 추출된 데이터를 SQL에서 연결**

### 해결 전략

#### 1-1. 프로젝트 식별자 통일

**공통 식별자**: Revit 파일명 기반

```
Revit 파일: 배관테스트.rvt
  ↓
프로젝트 코드: PIPE_TEST (자동 생성)
  ↓
Revit 데이터: project_id = PIPE_TEST, source = 'revit'
Navisworks 데이터: project_id = PIPE_TEST, source = 'navisworks'
```

**파일명 → 프로젝트 코드 변환 규칙**:

```python
def generate_project_code(filename: str) -> str:
    """
    Revit 파일명을 프로젝트 코드로 변환
    예: 배관테스트.rvt → PIPE_TEST
        Snowdon Towers.rvt → SNOWDON_TOWERS
    """
    # 1. 확장자 제거
    name = filename.replace('.rvt', '').replace('.nwc', '')

    # 2. 한글을 영문으로 변환 (선택사항)
    # hangul_to_english = {
    #     '배관': 'PIPE',
    #     '테스트': 'TEST',
    #     # ... 매핑 테이블
    # }

    # 3. 공백을 언더스코어로, 대문자 변환
    code = name.replace(' ', '_').replace('-', '_').upper()

    # 4. 특수문자 제거
    code = ''.join(c for c in code if c.isalnum() or c == '_')

    return code
```

#### 1-2. 통합 데이터 스키마

```sql
-- ============================================
-- 프로젝트 마스터 테이블
-- ============================================
CREATE TABLE projects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- 프로젝트 식별
    code VARCHAR(50) NOT NULL UNIQUE,  -- PIPE_TEST, SNOWDON_TOWERS
    name VARCHAR(255) NOT NULL,        -- 배관테스트, Snowdon Towers

    -- 소스 파일 정보
    revit_file_name VARCHAR(255),      -- 배관테스트.rvt
    revit_file_path TEXT,              -- C:\Users\...\배관테스트.rvt

    -- 프로젝트 정보 (Revit ProjectInfo)
    project_number VARCHAR(100),       -- 프로젝트 번호
    client_name VARCHAR(255),          -- 소유자
    address TEXT,                      -- 주소

    -- 메타데이터
    created_by VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    is_active BOOLEAN DEFAULT true,

    CONSTRAINT chk_project_code CHECK (code ~ '^[A-Z0-9_]+$')
);

-- ============================================
-- 리비전 마스터 테이블
-- ============================================
CREATE TABLE revisions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,

    -- 리비전 정보
    revision_number INTEGER NOT NULL,  -- 1, 2, 3, ...
    version_tag VARCHAR(50),           -- v1.0, RC1, DESIGN_PHASE
    description TEXT,                  -- 변경 설명

    -- 소스 정보
    source_type VARCHAR(20) NOT NULL,  -- 'revit' | 'navisworks'
    source_file_path TEXT,             -- 실제 파일 경로
    source_file_hash VARCHAR(64),      -- 파일 무결성 검증 (SHA256)

    -- 통계
    total_objects INTEGER DEFAULT 0,
    total_categories INTEGER DEFAULT 0,

    -- 변경 추적
    parent_revision_id UUID REFERENCES revisions(id),  -- 이전 리비전
    changes_summary JSONB,  -- {added: 10, modified: 5, deleted: 2}

    -- 메타데이터
    created_by VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    metadata JSONB DEFAULT '{}'::jsonb,

    CONSTRAINT uq_project_revision UNIQUE (project_id, revision_number),
    CONSTRAINT chk_source_type CHECK (source_type IN ('revit', 'navisworks'))
);

-- ============================================
-- 통합 객체 테이블 (Revit + Navisworks)
-- ============================================
CREATE TABLE unified_objects (
    id BIGSERIAL PRIMARY KEY,

    -- 프로젝트 및 리비전
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    revision_id UUID NOT NULL REFERENCES revisions(id) ON DELETE CASCADE,

    -- 객체 식별자
    object_id UUID NOT NULL,           -- Navisworks GUID 또는 Revit UniqueId
    element_id INTEGER,                -- Revit Element ID (Navisworks는 NULL)
    source_type VARCHAR(20) NOT NULL,  -- 'revit' | 'navisworks'

    -- ⭐ 계층 정보 (양쪽 모두 지원)
    parent_object_id UUID,             -- 부모 객체 ID
    level INTEGER DEFAULT 0,           -- 계층 깊이
    display_name VARCHAR(500),         -- 표시 이름
    spatial_path TEXT,                 -- Building > Level > Room

    -- 분류 정보
    category VARCHAR(255) NOT NULL,
    family VARCHAR(255),               -- Revit만
    type VARCHAR(255),                 -- Revit만

    -- 스케줄 연계
    activity_id VARCHAR(100),          -- 4D 시뮬레이션용

    -- 속성 데이터 (JSONB)
    properties JSONB NOT NULL DEFAULT '{}'::jsonb,
    bounding_box JSONB,

    -- 상태 추적
    change_type VARCHAR(20) DEFAULT 'added',
    previous_object_id BIGINT REFERENCES unified_objects(id),

    -- 메타데이터
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    CONSTRAINT chk_source_type CHECK (source_type IN ('revit', 'navisworks')),
    CONSTRAINT chk_change_type CHECK (change_type IN ('added', 'modified', 'deleted', 'unchanged')),
    CONSTRAINT uq_revision_object UNIQUE (revision_id, object_id)
);
```

#### 1-3. 데이터 매핑 전략

**Revit → Navisworks 객체 매칭**:

```python
# DXrevit에서 추출한 데이터
revit_object = {
    "object_id": "e3e052f9-0156-11d5-9301-0000863f27ad-00000017",  # UniqueId
    "element_id": 23,
    "category": "재료",
    "family": "기본값",
    "source_type": "revit",
    "parent_object_id": None,  # ← 추가 필요
    "level": 0,                # ← 추가 필요
}

# DXnavis에서 추출한 데이터
navisworks_object = {
    "object_id": "049dab74-be6f-4a10-906d-ca7a027aa210",  # InstanceGuid
    "parent_object_id": "00000000-0000-0000-0000-000000000000",
    "level": 5,
    "display_name": "Flex Pipe Round",
    "category": "Flex Pipes",
    "source_type": "navisworks",
    "properties": {
        "소스 파일": "배관테스트.rvt",
        "Element ID": "???",  # Revit Element ID와 매칭 가능!
    }
}
```

**매칭 전략**:

1. **Element ID 기반 매칭** (우선순위 1)
   ```sql
   -- Navisworks 속성에서 Element ID 추출
   SELECT
       n.object_id AS navis_object_id,
       r.object_id AS revit_object_id,
       n.display_name,
       r.category
   FROM unified_objects n
   JOIN unified_objects r
       ON r.element_id = (n.properties->>'Element ID')::INTEGER
   WHERE n.source_type = 'navisworks'
     AND r.source_type = 'revit'
     AND n.project_id = r.project_id;
   ```

2. **Category + Name 기반 매칭** (우선순위 2)
   ```sql
   -- 카테고리와 이름으로 매칭
   SELECT *
   FROM unified_objects n
   JOIN unified_objects r
       ON n.category = r.category
       AND n.display_name = r.family
   WHERE n.source_type = 'navisworks'
     AND r.source_type = 'revit';
   ```

3. **공간 좌표 기반 매칭** (우선순위 3)
   ```sql
   -- Bounding Box 중심점 거리 기반
   -- (복잡하지만 가장 정확)
   ```

#### 1-4. 통합 뷰 생성

```sql
-- ============================================
-- Revit + Navisworks 통합 뷰
-- ============================================
CREATE VIEW v_integrated_objects AS
SELECT
    p.code AS project_code,
    p.name AS project_name,
    r.revision_number,
    r.version_tag,

    -- Revit 데이터
    revit.object_id AS revit_object_id,
    revit.element_id,
    revit.category AS revit_category,
    revit.family,
    revit.type,
    revit.properties AS revit_properties,

    -- Navisworks 데이터
    navis.object_id AS navis_object_id,
    navis.parent_object_id,
    navis.level,
    navis.display_name AS navis_display_name,
    navis.spatial_path,
    navis.properties AS navis_properties,

    -- 매칭 상태
    CASE
        WHEN revit.object_id IS NOT NULL AND navis.object_id IS NOT NULL THEN 'matched'
        WHEN revit.object_id IS NOT NULL THEN 'revit_only'
        WHEN navis.object_id IS NOT NULL THEN 'navis_only'
    END AS match_status

FROM projects p
JOIN revisions r ON p.id = r.project_id
LEFT JOIN unified_objects revit
    ON r.id = revit.revision_id
    AND revit.source_type = 'revit'
LEFT JOIN unified_objects navis
    ON r.id = navis.revision_id
    AND navis.source_type = 'navisworks'
    AND navis.properties->>'Element ID' = revit.element_id::TEXT;
```

---

## 질문 2: 프로젝트명 기반 스키마 및 리비전 관리

### 문제 정의

**현재 방식**:
```
model_version = "프로젝트 이름_20251016_030006"
                 ↑ 파싱 필요, 일관성 없음
```

**목표**:
- 프로젝트명 명확한 식별
- 리비전 자동 관리
- 사용자 친화적 인터페이스

### 해결 전략

#### 2-1. 프로젝트 생성 워크플로우

**자동 프로젝트 감지 및 생성**:

```csharp
// DXrevit/Services/ProjectManager.cs
public class ProjectManager
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Revit 문서에서 프로젝트 정보 추출 및 등록
    /// </summary>
    public async Task<ProjectInfo> RegisterOrGetProject(Document document)
    {
        // 1. Revit 파일명에서 프로젝트 코드 생성
        string fileName = Path.GetFileNameWithoutExtension(document.PathName);
        string projectCode = GenerateProjectCode(fileName);

        // 2. 프로젝트 존재 확인
        var existingProject = await CheckProjectExists(projectCode);
        if (existingProject != null)
        {
            return existingProject;
        }

        // 3. 새 프로젝트 생성 (사용자 확인)
        var projectInfo = new ProjectInfo
        {
            Code = projectCode,
            Name = fileName,
            RevitFileName = fileName + ".rvt",
            RevitFilePath = document.PathName,
            ProjectNumber = document.ProjectInformation.Number,
            ClientName = document.ProjectInformation.ClientName,
            Address = document.ProjectInformation.Address,
            CreatedBy = Environment.UserName
        };

        // 4. API 서버에 프로젝트 등록
        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/projects",
            projectInfo
        );

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsAsync<ProjectInfo>();
        }

        throw new Exception("프로젝트 등록 실패");
    }

    /// <summary>
    /// 파일명을 프로젝트 코드로 변환
    /// </summary>
    private string GenerateProjectCode(string fileName)
    {
        // 공백, 하이픈을 언더스코어로
        string code = fileName
            .Replace(" ", "_")
            .Replace("-", "_")
            .ToUpperInvariant();

        // 특수문자 제거
        code = Regex.Replace(code, @"[^A-Z0-9_]", "");

        // 길이 제한 (최대 50자)
        if (code.Length > 50)
        {
            code = code.Substring(0, 50);
        }

        return code;
    }
}
```

#### 2-2. 리비전 자동 관리

**리비전 생성 로직**:

```csharp
// DXrevit/Services/RevisionManager.cs
public class RevisionManager
{
    /// <summary>
    /// 새 리비전 생성 (자동 번호 할당)
    /// </summary>
    public async Task<RevisionInfo> CreateRevision(
        string projectCode,
        string versionTag,
        string description)
    {
        // 1. 최신 리비전 번호 조회
        var latestRevision = await GetLatestRevision(projectCode);
        int nextRevisionNumber = (latestRevision?.RevisionNumber ?? 0) + 1;

        // 2. 파일 해시 계산 (중복 체크용)
        string fileHash = CalculateFileHash(document.PathName);

        // 3. 리비전 정보 생성
        var revisionInfo = new RevisionInfo
        {
            ProjectCode = projectCode,
            RevisionNumber = nextRevisionNumber,
            VersionTag = versionTag,  // 사용자 입력 또는 자동 생성
            Description = description,
            SourceType = "revit",
            SourceFilePath = document.PathName,
            SourceFileHash = fileHash,
            CreatedBy = Environment.UserName
        };

        // 4. API 서버에 리비전 등록
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/projects/{projectCode}/revisions",
            revisionInfo
        );

        return await response.Content.ReadAsAsync<RevisionInfo>();
    }

    /// <summary>
    /// 파일 해시 계산 (SHA256)
    /// </summary>
    private string CalculateFileHash(string filePath)
    {
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(filePath))
        {
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}
```

#### 2-3. UI 개선 (사용자 친화적)

**DXrevit 스냅샷 UI 개선**:

```xml
<!-- DXrevit/Views/SnapshotView.xaml -->
<StackPanel>
    <!-- 프로젝트 정보 (자동 감지) -->
    <GroupBox Header="프로젝트 정보">
        <StackPanel>
            <TextBlock Text="프로젝트 코드:" FontWeight="Bold"/>
            <TextBlock Text="{Binding ProjectCode}" Foreground="Blue"/>

            <TextBlock Text="파일명:" FontWeight="Bold" Margin="0,10,0,0"/>
            <TextBlock Text="{Binding FileName}"/>

            <TextBlock Text="현재 리비전:" FontWeight="Bold" Margin="0,10,0,0"/>
            <TextBlock Text="{Binding CurrentRevisionNumber}"/>
        </StackPanel>
    </GroupBox>

    <!-- 리비전 정보 (사용자 입력) -->
    <GroupBox Header="새 리비전 정보" Margin="0,10,0,0">
        <StackPanel>
            <TextBlock Text="버전 태그:" FontWeight="Bold"/>
            <ComboBox SelectedItem="{Binding VersionTag}" IsEditable="True">
                <ComboBoxItem Content="v1.0"/>
                <ComboBoxItem Content="v1.1"/>
                <ComboBoxItem Content="RC1"/>
                <ComboBoxItem Content="DESIGN_PHASE"/>
                <ComboBoxItem Content="CONSTRUCTION"/>
            </ComboBox>

            <TextBlock Text="변경 설명:" FontWeight="Bold" Margin="0,10,0,0"/>
            <TextBox Text="{Binding Description}" Height="60" TextWrapping="Wrap"
                     AcceptsReturn="True" VerticalScrollBarVisibility="Auto"/>
        </StackPanel>
    </GroupBox>

    <!-- 실행 버튼 -->
    <Button Content="스냅샷 생성" Command="{Binding CreateSnapshotCommand}"
            Margin="0,10,0,0" Height="40" FontSize="16"/>
</StackPanel>
```

**DXnavis 계층 추출 UI 개선**:

```xml
<!-- DXnavis/Views/DXwindow.xaml -->
<StackPanel>
    <!-- 프로젝트 자동 감지 -->
    <GroupBox Header="프로젝트 정보 (자동 감지)">
        <StackPanel>
            <TextBlock Text="소스 Revit 파일:" FontWeight="Bold"/>
            <TextBlock Text="{Binding SourceRevitFile}" Foreground="Blue"/>

            <TextBlock Text="프로젝트 코드:" FontWeight="Bold" Margin="0,10,0,0"/>
            <TextBlock Text="{Binding ProjectCode}" Foreground="Green"/>
        </StackPanel>
    </GroupBox>

    <!-- 리비전 선택 -->
    <GroupBox Header="리비전 선택" Margin="0,10,0,0">
        <StackPanel>
            <TextBlock Text="기존 리비전에 추가 또는 새 리비전 생성"/>
            <RadioButton Content="최신 리비전에 추가" IsChecked="True"
                         GroupName="RevisionOption"/>
            <RadioButton Content="새 리비전 생성" Margin="0,5,0,0"
                         GroupName="RevisionOption"/>

            <TextBox Text="{Binding NewVersionTag}" Margin="0,10,0,0"
                     PlaceholderText="새 버전 태그 (예: v1.1)"/>
        </StackPanel>
    </GroupBox>

    <!-- 계층 추출 버튼 -->
    <Button Content="계층 정보 추출 및 업로드" Command="{Binding ExtractHierarchyCommand}"
            Margin="0,10,0,0" Height="40" FontSize="16"/>
</StackPanel>
```

---

## 질문 3: Navisworks 계층 데이터 업로드

### 문제 정의

**현재**: Navisworks 계층 데이터가 CSV로만 저장, SQL 업로드 안됨

**목표**: CSV → SQL 자동 업로드

### 해결 전략

#### 3-1. API 엔드포인트 추가

```python
# fastapi_server/routers/navisworks.py
from fastapi import APIRouter, File, UploadFile, HTTPException
from typing import List
import csv
import io

router = APIRouter(prefix="/api/v1/navisworks", tags=["navisworks"])


@router.post("/projects/{project_code}/revisions/{revision_number}/hierarchy")
async def upload_hierarchy_data(
    project_code: str,
    revision_number: int,
    file: UploadFile = File(...),
    db: AsyncDatabase = Depends(get_db)
):
    """
    Navisworks 계층 데이터 업로드 (CSV)
    """
    # 1. 프로젝트 및 리비전 확인
    project = await db.fetchrow(
        "SELECT id FROM projects WHERE code = $1",
        project_code
    )
    if not project:
        raise HTTPException(404, "프로젝트를 찾을 수 없습니다")

    revision = await db.fetchrow(
        """
        SELECT id FROM revisions
        WHERE project_id = $1 AND revision_number = $2
        """,
        project['id'], revision_number
    )
    if not revision:
        raise HTTPException(404, "리비전을 찾을 수 없습니다")

    # 2. CSV 파일 읽기
    contents = await file.read()
    decoded = contents.decode('utf-8-sig')  # BOM 제거
    csv_reader = csv.DictReader(io.StringIO(decoded))

    # 3. 데이터 변환 및 삽입
    objects_to_insert = []
    for row in csv_reader:
        # CSV 행 → unified_objects 변환
        obj = {
            'project_id': project['id'],
            'revision_id': revision['id'],
            'object_id': row['ObjectId'],
            'parent_object_id': row['ParentId'] if row['ParentId'] != '00000000-0000-0000-0000-000000000000' else None,
            'level': int(row['Level']),
            'display_name': row['DisplayName'],
            'source_type': 'navisworks',
            'category': row['Category'],
            'properties': {
                row['PropertyName']: row['PropertyValue']
            }
        }
        objects_to_insert.append(obj)

    # 4. 배치 삽입
    # (속성별로 행이 나뉘므로 집계 필요)
    aggregated = aggregate_properties(objects_to_insert)

    async with db.pool.acquire() as conn:
        async with conn.transaction():
            for obj in aggregated:
                await conn.execute(
                    """
                    INSERT INTO unified_objects (
                        project_id, revision_id, object_id, parent_object_id,
                        level, display_name, source_type, category, properties
                    ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                    ON CONFLICT (revision_id, object_id) DO UPDATE
                    SET properties = unified_objects.properties || EXCLUDED.properties
                    """,
                    obj['project_id'], obj['revision_id'], obj['object_id'],
                    obj['parent_object_id'], obj['level'], obj['display_name'],
                    obj['source_type'], obj['category'], json.dumps(obj['properties'])
                )

    return {
        "message": "계층 데이터 업로드 완료",
        "objects_count": len(aggregated)
    }


def aggregate_properties(objects: List[dict]) -> List[dict]:
    """
    EAV 패턴 데이터를 객체별로 집계
    """
    aggregated = {}
    for obj in objects:
        obj_id = obj['object_id']
        if obj_id not in aggregated:
            aggregated[obj_id] = {
                'project_id': obj['project_id'],
                'revision_id': obj['revision_id'],
                'object_id': obj_id,
                'parent_object_id': obj['parent_object_id'],
                'level': obj['level'],
                'display_name': obj['display_name'],
                'source_type': obj['source_type'],
                'category': obj['category'],
                'properties': {}
            }
        # 속성 병합
        aggregated[obj_id]['properties'].update(obj['properties'])

    return list(aggregated.values())
```

#### 3-2. DXnavis 플러그인 수정

```csharp
// DXnavis/Services/HierarchyUploader.cs
public class HierarchyUploader
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// CSV 파일을 서버에 업로드
    /// </summary>
    public async Task UploadHierarchyData(
        string projectCode,
        int revisionNumber,
        string csvFilePath)
    {
        using (var fileStream = File.OpenRead(csvFilePath))
        using (var content = new MultipartFormDataContent())
        {
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(streamContent, "file", Path.GetFileName(csvFilePath));

            var response = await _httpClient.PostAsync(
                $"/api/v1/navisworks/projects/{projectCode}/revisions/{revisionNumber}/hierarchy",
                content
            );

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                MessageBox.Show($"계층 데이터 업로드 완료!\n{result}");
            }
            else
            {
                throw new Exception($"업로드 실패: {response.StatusCode}");
            }
        }
    }
}

// DXnavis/ViewModels/DXwindowViewModel.cs
public class DXwindowViewModel
{
    private HierarchyUploader _uploader;

    public async Task ExtractAndUploadHierarchy()
    {
        // 1. 계층 추출 (기존 로직)
        var csvFilePath = await ExtractHierarchyToCsv();

        // 2. 프로젝트 코드 자동 감지
        string projectCode = DetectProjectCode();

        // 3. 리비전 번호 가져오기 (사용자 선택 또는 자동)
        int revisionNumber = await GetOrCreateRevision(projectCode);

        // 4. 서버에 업로드
        await _uploader.UploadHierarchyData(projectCode, revisionNumber, csvFilePath);

        MessageBox.Show("계층 데이터가 SQL 데이터베이스에 저장되었습니다!");
    }

    private string DetectProjectCode()
    {
        // Navisworks 파일에서 소스 Revit 파일명 추출
        // 예: 배관테스트.rvt → PIPE_TEST
        var sourceFile = GetSourceRevitFileName();
        return GenerateProjectCode(sourceFile);
    }
}
```

---

## 질문 4: 메타데이터 구조 및 데이터 파이프라인

### 4-1. 명확한 메타데이터 구조

#### 계층 구조

```
┌─────────────────────────────────────────────────────────┐
│ Layer 1: Projects (프로젝트)                             │
│  - 프로젝트 마스터 정보                                   │
│  - 파일명, 코드, 클라이언트 정보                          │
└──────────────┬──────────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────────┐
│ Layer 2: Revisions (리비전)                              │
│  - 버전 이력 관리                                        │
│  - 소스 파일, 변경 내역                                  │
└──────────────┬──────────────────────────────────────────┘
               │
               ├─────────────┬─────────────┐
               ▼             ▼             ▼
┌──────────────────┐ ┌──────────────┐ ┌──────────────┐
│ Layer 3: Objects │ │ Relationships│ │ Activities   │
│  - Revit 객체    │ │  - 관계 정보  │ │  - 스케줄    │
│  - Navis 계층    │ │  - 연결 구조  │ │  - 4D 정보   │
└──────────────────┘ └──────────────┘ └──────────────┘
```

#### 메타데이터 테이블 정의

```sql
-- ============================================
-- 1. Projects 테이블 (프로젝트 마스터)
-- ============================================
CREATE TABLE projects (
    -- 식별자
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) NOT NULL UNIQUE,        -- PIPE_TEST
    name VARCHAR(255) NOT NULL,              -- 배관테스트

    -- 파일 정보
    revit_file_name VARCHAR(255),            -- 배관테스트.rvt
    revit_file_path TEXT,

    -- 프로젝트 정보 (Revit ProjectInfo)
    project_number VARCHAR(100),             -- 프로젝트 번호
    client_name VARCHAR(255),                -- 소유자
    address TEXT,                            -- 주소
    building_name VARCHAR(255),              -- 건물명

    -- 위치 정보 (Navisworks Location)
    latitude DOUBLE PRECISION,               -- 위도
    longitude DOUBLE PRECISION,              -- 경도
    elevation DOUBLE PRECISION,              -- 고도
    timezone INTEGER,                        -- 시간대

    -- 메타데이터
    created_by VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    is_active BOOLEAN DEFAULT true,

    -- 추가 정보 (JSONB)
    metadata JSONB DEFAULT '{}'::jsonb,

    CONSTRAINT chk_project_code CHECK (code ~ '^[A-Z0-9_]+$')
);

-- ============================================
-- 2. Revisions 테이블 (리비전 이력)
-- ============================================
CREATE TABLE revisions (
    -- 식별자
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,

    -- 리비전 정보
    revision_number INTEGER NOT NULL,        -- 1, 2, 3, ...
    version_tag VARCHAR(50),                 -- v1.0, RC1
    description TEXT,                        -- 변경 설명

    -- 소스 정보
    source_type VARCHAR(20) NOT NULL,        -- 'revit' | 'navisworks'
    source_file_path TEXT,
    source_file_hash VARCHAR(64),            -- SHA256

    -- 통계 정보
    total_objects INTEGER DEFAULT 0,
    total_categories INTEGER DEFAULT 0,

    -- 변경 추적
    parent_revision_id UUID REFERENCES revisions(id),
    changes_summary JSONB,  -- {added: 10, modified: 5, deleted: 2}

    -- 메타데이터
    created_by VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    metadata JSONB DEFAULT '{}'::jsonb,

    CONSTRAINT uq_project_revision UNIQUE (project_id, revision_number),
    CONSTRAINT chk_source_type CHECK (source_type IN ('revit', 'navisworks'))
);

-- ============================================
-- 3. Unified Objects 테이블 (통합 객체)
-- ============================================
-- (앞서 정의한 스키마 사용)

-- ============================================
-- 4. Activities 테이블 (스케줄/4D)
-- ============================================
CREATE TABLE activities (
    id BIGSERIAL PRIMARY KEY,
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,

    -- 활동 정보
    activity_id VARCHAR(100) NOT NULL UNIQUE,  -- WBS 코드
    activity_name VARCHAR(255) NOT NULL,       -- 작업명

    -- 스케줄 정보
    planned_start_date DATE,
    planned_end_date DATE,
    actual_start_date DATE,
    actual_end_date DATE,
    duration INTEGER,                          -- 일수
    progress DECIMAL(5, 2) DEFAULT 0.00,       -- 진행률 (%)

    -- 분류
    wbs_code VARCHAR(100),                     -- WBS 코드
    discipline VARCHAR(50),                    -- 공종

    -- 메타데이터
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    metadata JSONB DEFAULT '{}'::jsonb
);

-- ============================================
-- 5. Object-Activity 매핑 테이블
-- ============================================
CREATE TABLE object_activity_mappings (
    id BIGSERIAL PRIMARY KEY,
    object_id BIGINT NOT NULL REFERENCES unified_objects(id) ON DELETE CASCADE,
    activity_id BIGINT NOT NULL REFERENCES activities(id) ON DELETE CASCADE,

    -- 매핑 정보
    mapping_type VARCHAR(50),  -- 'direct', 'inherited', 'manual'
    confidence DECIMAL(3, 2),  -- 0.00 ~ 1.00 (매칭 신뢰도)

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    CONSTRAINT uq_object_activity UNIQUE (object_id, activity_id)
);
```

### 4-2. 데이터 파이프라인 설계

#### ELT vs ETL 비교

| 측면 | ETL (Extract-Transform-Load) | ELT (Extract-Load-Transform) |
|------|------------------------------|------------------------------|
| **처리 위치** | 외부 서버 (Airflow 등) | 데이터베이스 내부 (SQL) |
| **성능** | 외부 처리로 DB 부하 감소 | DB 연산 활용 (병렬 처리) |
| **유연성** | 복잡한 변환 가능 | SQL 제약 (단순 변환) |
| **유지보수** | 별도 파이프라인 관리 | SQL 쿼리만 관리 |
| **BI 도구 연동** | 중간 단계 필요 | 직접 연결 가능 |

**권장**: **ELT 방식**

**이유**:
1. ✅ PostgreSQL 강력한 JSONB 연산 지원
2. ✅ Power BI/Tableau가 직접 SQL 쿼리 가능
3. ✅ 별도 파이프라인 서버 불필요
4. ✅ 유지보수 단순 (SQL만 관리)

#### ELT 파이프라인 구조

```
┌──────────────────────────────────────────────────────────┐
│ Extract (추출)                                            │
├──────────────────────────────────────────────────────────┤
│ DXrevit → FastAPI → PostgreSQL (unified_objects)         │
│ DXnavis → FastAPI → PostgreSQL (unified_objects)         │
└──────────────────────────────────────────────────────────┘
                        ↓
┌──────────────────────────────────────────────────────────┐
│ Load (적재) - Raw Data                                    │
├──────────────────────────────────────────────────────────┤
│ unified_objects 테이블에 원본 데이터 저장                  │
│  - JSONB 형식으로 모든 속성 보존                          │
│  - 소스 타입 구분 (revit/navisworks)                      │
└──────────────────────────────────────────────────────────┘
                        ↓
┌──────────────────────────────────────────────────────────┐
│ Transform (변환) - SQL Views & Materialized Views         │
├──────────────────────────────────────────────────────────┤
│ 1. v_bi_objects: BI 도구용 평면 뷰                        │
│ 2. v_bi_hierarchy: 계층 구조 뷰                           │
│ 3. v_bi_4d_schedule: 4D 시뮬레이션 뷰                     │
│ 4. v_bi_project_summary: 프로젝트 요약 뷰                 │
└──────────────────────────────────────────────────────────┘
                        ↓
┌──────────────────────────────────────────────────────────┐
│ Serve (제공) - BI Tools                                   │
├──────────────────────────────────────────────────────────┤
│ Power BI ←─ PostgreSQL Connector                         │
│ Tableau  ←─ PostgreSQL Connector                         │
└──────────────────────────────────────────────────────────┘
```

#### BI용 뷰 생성

```sql
-- ============================================
-- 1. BI 객체 뷰 (평면화)
-- ============================================
CREATE MATERIALIZED VIEW v_bi_objects AS
SELECT
    -- 프로젝트 정보
    p.code AS project_code,
    p.name AS project_name,
    p.client_name,
    p.building_name,

    -- 리비전 정보
    r.revision_number,
    r.version_tag,
    r.created_at AS revision_date,

    -- 객체 정보
    o.object_id,
    o.element_id,
    o.source_type,
    o.display_name,
    o.category,
    o.family,
    o.type,

    -- 계층 정보
    o.parent_object_id,
    o.level,
    o.spatial_path,

    -- 스케줄 정보
    o.activity_id,
    a.activity_name,
    a.planned_start_date,
    a.planned_end_date,
    a.progress,

    -- 속성 (주요 속성만 추출)
    o.properties->>'이름' AS property_name,
    o.properties->>'유형' AS property_type,
    (o.properties->>'Element ID')::INTEGER AS element_id_from_navis,

    -- Bounding Box (공간 분석용)
    (o.bounding_box->>'MinX')::DOUBLE PRECISION AS bbox_min_x,
    (o.bounding_box->>'MinY')::DOUBLE PRECISION AS bbox_min_y,
    (o.bounding_box->>'MinZ')::DOUBLE PRECISION AS bbox_min_z,
    (o.bounding_box->>'MaxX')::DOUBLE PRECISION AS bbox_max_x,
    (o.bounding_box->>'MaxY')::DOUBLE PRECISION AS bbox_max_y,
    (o.bounding_box->>'MaxZ')::DOUBLE PRECISION AS bbox_max_z,

    -- 계산 필드
    CASE
        WHEN o.source_type = 'revit' AND EXISTS (
            SELECT 1 FROM unified_objects n
            WHERE n.source_type = 'navisworks'
              AND n.project_id = o.project_id
              AND n.properties->>'Element ID' = o.element_id::TEXT
        ) THEN 'matched'
        ELSE 'unmatched'
    END AS match_status

FROM unified_objects o
JOIN revisions r ON o.revision_id = r.id
JOIN projects p ON o.project_id = p.id
LEFT JOIN object_activity_mappings oam ON o.id = oam.object_id
LEFT JOIN activities a ON oam.activity_id = a.id
WHERE r.revision_number = (
    SELECT MAX(revision_number)
    FROM revisions
    WHERE project_id = r.project_id
);

-- 인덱스 생성 (성능 최적화)
CREATE INDEX idx_bi_objects_project ON v_bi_objects(project_code);
CREATE INDEX idx_bi_objects_category ON v_bi_objects(category);
CREATE INDEX idx_bi_objects_activity ON v_bi_objects(activity_id);

-- ============================================
-- 2. BI 계층 뷰 (트리 구조)
-- ============================================
CREATE MATERIALIZED VIEW v_bi_hierarchy AS
WITH RECURSIVE hierarchy AS (
    -- 루트 노드 (Level 0)
    SELECT
        o.object_id,
        o.parent_object_id,
        o.level,
        o.display_name,
        o.category,
        o.display_name::TEXT AS hierarchy_path,
        p.code AS project_code,
        r.revision_number
    FROM unified_objects o
    JOIN revisions r ON o.revision_id = r.id
    JOIN projects p ON o.project_id = p.id
    WHERE o.level = 0
      AND o.source_type = 'navisworks'

    UNION ALL

    -- 자식 노드
    SELECT
        o.object_id,
        o.parent_object_id,
        o.level,
        o.display_name,
        o.category,
        h.hierarchy_path || ' > ' || o.display_name,
        h.project_code,
        h.revision_number
    FROM unified_objects o
    JOIN hierarchy h ON o.parent_object_id = h.object_id
    JOIN revisions r ON o.revision_id = r.id
    WHERE o.source_type = 'navisworks'
)
SELECT * FROM hierarchy;

-- ============================================
-- 3. BI 4D 스케줄 뷰
-- ============================================
CREATE MATERIALIZED VIEW v_bi_4d_schedule AS
SELECT
    p.code AS project_code,
    p.name AS project_name,

    -- 활동 정보
    a.activity_id,
    a.activity_name,
    a.wbs_code,
    a.discipline,

    -- 스케줄
    a.planned_start_date,
    a.planned_end_date,
    a.actual_start_date,
    a.actual_end_date,
    a.duration,
    a.progress,

    -- 연결된 객체 수
    COUNT(DISTINCT oam.object_id) AS linked_objects_count,

    -- 카테고리별 객체 수
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.category = '벽') AS wall_count,
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.category = '기둥') AS column_count,
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.category = '배관') AS pipe_count,

    -- 진행 상태
    CASE
        WHEN a.actual_end_date IS NOT NULL THEN 'Completed'
        WHEN a.actual_start_date IS NOT NULL THEN 'In Progress'
        WHEN a.planned_start_date > CURRENT_DATE THEN 'Upcoming'
        ELSE 'Not Started'
    END AS status

FROM activities a
JOIN projects p ON a.project_id = p.id
LEFT JOIN object_activity_mappings oam ON a.id = oam.activity_id
LEFT JOIN unified_objects o ON oam.object_id = o.id
GROUP BY p.code, p.name, a.id, a.activity_id, a.activity_name, a.wbs_code,
         a.discipline, a.planned_start_date, a.planned_end_date,
         a.actual_start_date, a.actual_end_date, a.duration, a.progress;

-- ============================================
-- 4. BI 프로젝트 요약 뷰
-- ============================================
CREATE MATERIALIZED VIEW v_bi_project_summary AS
SELECT
    p.code AS project_code,
    p.name AS project_name,
    p.client_name,
    p.building_name,

    -- 최신 리비전 정보
    (SELECT MAX(revision_number) FROM revisions WHERE project_id = p.id) AS latest_revision,
    (SELECT MAX(created_at) FROM revisions WHERE project_id = p.id) AS last_updated,

    -- 객체 통계
    (SELECT COUNT(*) FROM unified_objects o
     JOIN revisions r ON o.revision_id = r.id
     WHERE r.project_id = p.id
       AND o.source_type = 'revit') AS revit_objects_count,

    (SELECT COUNT(*) FROM unified_objects o
     JOIN revisions r ON o.revision_id = r.id
     WHERE r.project_id = p.id
       AND o.source_type = 'navisworks') AS navisworks_objects_count,

    (SELECT COUNT(DISTINCT category) FROM unified_objects o
     JOIN revisions r ON o.revision_id = r.id
     WHERE r.project_id = p.id) AS categories_count,

    -- 활동 통계
    (SELECT COUNT(*) FROM activities WHERE project_id = p.id) AS total_activities,
    (SELECT COUNT(*) FROM activities
     WHERE project_id = p.id AND actual_end_date IS NOT NULL) AS completed_activities,

    -- 진행률
    (SELECT AVG(progress) FROM activities WHERE project_id = p.id) AS overall_progress

FROM projects p
WHERE p.is_active = true;
```

#### 자동 새로고침 (Materialized View)

```sql
-- ============================================
-- Materialized View 새로고침 함수
-- ============================================
CREATE OR REPLACE FUNCTION refresh_bi_views()
RETURNS void AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY v_bi_objects;
    REFRESH MATERIALIZED VIEW CONCURRENTLY v_bi_hierarchy;
    REFRESH MATERIALIZED VIEW CONCURRENTLY v_bi_4d_schedule;
    REFRESH MATERIALIZED VIEW CONCURRENTLY v_bi_project_summary;
END;
$$ LANGUAGE plpgsql;

-- ============================================
-- 자동 새로고침 트리거 (새 리비전 생성 시)
-- ============================================
CREATE OR REPLACE FUNCTION trigger_refresh_bi_views()
RETURNS TRIGGER AS $$
BEGIN
    -- 비동기로 새로고침 (pg_cron 사용 권장)
    PERFORM refresh_bi_views();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER after_revision_insert
AFTER INSERT ON revisions
FOR EACH ROW
EXECUTE FUNCTION trigger_refresh_bi_views();
```

#### Power BI 연결 방법

```python
# Power BI에서 PostgreSQL 연결
# 1. Power BI Desktop 열기
# 2. 데이터 가져오기 > PostgreSQL
# 3. 연결 정보 입력:
#    - Server: localhost
#    - Database: DX_platform
# 4. 뷰 선택:
#    - v_bi_objects
#    - v_bi_hierarchy
#    - v_bi_4d_schedule
#    - v_bi_project_summary
```

---

## 통합 실행 계획

### Phase 1: 데이터베이스 스키마 구축 (1주)

```bash
# 1. 새 스키마 생성
psql -U postgres -d DX_platform -f database/migrations/002_integrated_schema.sql

# 2. 뷰 생성
psql -U postgres -d DX_platform -f database/migrations/003_bi_views.sql

# 3. 테스트 데이터 삽입
python scripts/test_integrated_schema.py
```

### Phase 2: DXrevit 플러그인 개선 (1주)

```csharp
// 수정 파일:
// 1. DXrevit/Services/ProjectManager.cs (새로 추가)
// 2. DXrevit/Services/RevisionManager.cs (새로 추가)
// 3. DXrevit/Services/DataExtractor.cs (계층 정보 추가)
// 4. DXrevit/Views/SnapshotView.xaml (UI 개선)
// 5. DXrevit/ViewModels/SnapshotViewModel.cs (비즈니스 로직)
```

### Phase 3: DXnavis 플러그인 개선 (1주)

```csharp
// 수정 파일:
// 1. DXnavis/Services/HierarchyUploader.cs (새로 추가)
// 2. DXnavis/Services/NavisworksDataExtractor.cs (프로젝트 감지)
// 3. DXnavis/Views/DXwindow.xaml (UI 개선)
// 4. DXnavis/ViewModels/DXwindowViewModel.cs (업로드 로직)
```

### Phase 4: FastAPI 엔드포인트 추가 (3일)

```python
# 새로 추가:
# 1. fastapi_server/routers/projects.py
# 2. fastapi_server/routers/revisions.py
# 3. fastapi_server/routers/navisworks.py
# 4. fastapi_server/routers/bi.py (BI 뷰 조회용)
```

### Phase 5: 테스트 및 검증 (1주)

```bash
# 1. 배관테스트.rvt 파일로 테스트
# - Revit에서 스냅샷 생성 → SQL 확인
# - Navisworks에서 계층 추출 → SQL 확인
# - 통합 뷰 조회 확인

# 2. Power BI 연결 테스트
# - v_bi_objects 뷰로 대시보드 생성
# - v_bi_4d_schedule 뷰로 간트 차트 생성
```

---

## 요약 및 권장사항

### 핵심 결정사항

1. ✅ **프로젝트 식별**: Revit 파일명 → 프로젝트 코드 자동 생성
2. ✅ **리비전 관리**: 자동 번호 할당, 사용자 친화적 UI
3. ✅ **Navisworks 업로드**: CSV → API → SQL 자동화
4. ✅ **데이터 파이프라인**: ELT 방식, Materialized Views
5. ✅ **BI 연동**: PostgreSQL Connector 직접 연결

### 즉시 시작 가능한 작업

**데이터베이스**:
```sql
-- 통합 스키마 생성 스크립트 실행
\i database/migrations/002_integrated_schema.sql
```

**플러그인**:
- DXrevit: ProjectManager, RevisionManager 클래스 추가
- DXnavis: HierarchyUploader 클래스 추가

**FastAPI**:
- /api/v1/projects 엔드포인트 추가
- /api/v1/navisworks/hierarchy 엔드포인트 추가

---

**작성자**: System Integration Team
**최종 수정**: 2025-10-18
