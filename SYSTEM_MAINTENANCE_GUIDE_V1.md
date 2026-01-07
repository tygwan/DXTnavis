# AWP 2025 시스템 유지보수 가이드 v1.0

**버전**: 1.0
**작성일**: 2025-10-18
**상태**: 현재 시스템 기준 (4가지 통합 개선 이전)
**다음 버전**: v2.0 (통합 개선 완료 후)

---

## 📋 목차

1. [시스템 개요](#시스템-개요)
2. [Revit 플러그인 (DXrevit)](#revit-플러그인-dxrevit)
3. [Navisworks 플러그인 (DXnavis)](#navisworks-플러그인-dxnavis)
4. [FastAPI 서버](#fastapi-서버)
5. [PostgreSQL 데이터베이스](#postgresql-데이터베이스)
6. [상호작용 워크플로우](#상호작용-워크플로우)
7. [트러블슈팅](#트러블슈팅)
8. [v2.0 업그레이드 준비](#v20-업그레이드-준비)

---

## 시스템 개요

### 전체 아키텍처 (v1.0 현재)

```
┌─────────────────────────────────────────────────────────────┐
│ Revit 2025                                                  │
│  └─ DXrevit Plugin                                          │
│     ├─ 스냅샷 생성 (Element 추출)                            │
│     ├─ Properties → JSONB                                   │
│     └─ API 전송 (objects 테이블)                            │
└─────────────┬───────────────────────────────────────────────┘
              │ HTTP POST
              ▼
┌─────────────────────────────────────────────────────────────┐
│ FastAPI Server (로컬/원격)                                   │
│  ├─ POST /api/v1/snapshot                                   │
│  ├─ GET /api/v1/system/health                               │
│  └─ Middleware: CORS, Error Handling                        │
└─────────────┬───────────────────────────────────────────────┘
              │ asyncpg
              ▼
┌─────────────────────────────────────────────────────────────┐
│ PostgreSQL (localhost:5432)                                 │
│  ├─ Database: DX_platform                                   │
│  ├─ Tables: metadata, objects, relationships                │
│  └─ User: postgres / 123456                                 │
└─────────────────────────────────────────────────────────────┘
              ▲
              │ CSV Export (수동)
┌─────────────┴───────────────────────────────────────────────┐
│ Navisworks 2025                                             │
│  └─ DXnavis Plugin                                          │
│     ├─ 계층 추출 (Hierarchy)                                 │
│     ├─ Properties → CSV                                     │
│     └─ 파일 저장 (SQL 업로드 ❌)                             │
└─────────────────────────────────────────────────────────────┘
```

### 주요 구성요소

| 구성요소 | 버전 | 위치 | 역할 |
|---------|------|------|------|
| **DXrevit** | v1.0 | `개발폴더/DXrevit/` | Revit 데이터 추출 및 전송 |
| **DXnavis** | v1.0 | `개발폴더/DXnavis/` | Navisworks 계층 추출 (CSV) |
| **FastAPI** | v1.0 | `개발폴더/fastapi_server/` | API 서버 |
| **PostgreSQL** | 15 | localhost:5432 | 데이터베이스 |
**postgresql의 현재 개발 버전은 17을 사용중이다.**
---

## Revit 플러그인 (DXrevit)

### 1. 설치 및 설정

#### 1-1. 필수 요구사항

**소프트웨어**:
- Autodesk Revit 2025
- .NET Framework 4.8 또는 .NET 8.0
- Visual Studio 2022 (개발 시)

**Revit API 참조**:
```xml
<!-- DXrevit/DXrevit.csproj -->
<ItemGroup>
  <Reference Include="RevitAPI">
    <HintPath>C:\Program Files\Autodesk\Revit 2025\RevitAPI.dll</HintPath>
  </Reference>
  <Reference Include="RevitAPIUI">
    <HintPath>C:\Program Files\Autodesk\Revit 2025\RevitAPIUI.dll</HintPath>
  </Reference>
</ItemGroup>
```

#### 1-2. 플러그인 설치

**방법 1: 자동 설치 (빌드 후 복사)**

```xml
<!-- DXrevit.csproj - PostBuild 이벤트 -->
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="
    xcopy &quot;$(TargetPath)&quot; &quot;$(AppData)\Autodesk\Revit\Addins\2025\&quot; /Y
    xcopy &quot;$(TargetDir)*.dll&quot; &quot;$(AppData)\Autodesk\Revit\Addins\2025\&quot; /Y
    xcopy &quot;$(ProjectDir)DXrevit.addin&quot; &quot;$(AppData)\Autodesk\Revit\Addins\2025\&quot; /Y
  " />
</Target>
```

**방법 2: 수동 설치**

```bash
# 1. 빌드된 파일 복사
xcopy "개발폴더\DXrevit\bin\Release\net8.0-windows\*.*" ^
      "%AppData%\Autodesk\Revit\Addins\2025\" /Y

# 2. .addin 매니페스트 파일 복사
xcopy "개발폴더\DXrevit\DXrevit.addin" ^
      "%AppData%\Autodesk\Revit\Addins\2025\" /Y
```

**설치 경로**:
```
C:\Users\[사용자명]\AppData\Roaming\Autodesk\Revit\Addins\2025\
├─ DXrevit.dll
├─ DXrevit.addin
├─ DXBase.dll
└─ 기타 의존성 DLL
```

#### 1-3. 매니페스트 파일 (.addin)

```xml
<!-- DXrevit.addin -->
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>DXrevit</Name>
    <Assembly>DXrevit.dll</Assembly>
    <AddInId>8dd55e0a-2aee-5612-8465-b8f7ff0e7da3</AddInId>
    <FullClassName>DXrevit.Application</FullClassName>
    <VendorId>ADSK</VendorId>
    <VendorDescription>DX Platform</VendorDescription>
  </AddIn>
</RevitAddIns>
```

**주의사항**:
- `AddInId`는 GUID 형식, 고유해야 함
- `Assembly`는 DLL 파일명과 일치
- `FullClassName`은 Application 클래스의 전체 네임스페이스

#### 1-4. 설정 파일

**위치**: `DXrevit/Properties/Settings.settings`

```xml
<?xml version='1.0' encoding='utf-8'?>
<SettingsFile>
  <Profiles />
  <Settings>
    <Setting Name="SERVER_URL" Type="System.String" Scope="User">
      <Value Profile="(Default)">http://localhost:8000</Value>
    </Setting>
    <Setting Name="API_VERSION" Type="System.String" Scope="User">
      <Value Profile="(Default)">v1</Value>
    </Setting>
    <Setting Name="TIMEOUT_SECONDS" Type="System.Int32" Scope="User">
      <Value Profile="(Default)">30</Value>
    </Setting>
  </Settings>
</SettingsFile>
```

**설정 변경 방법**:

```csharp
// 코드에서 변경
DXrevit.Properties.Settings.Default.SERVER_URL = "http://192.168.1.100:8000";
DXrevit.Properties.Settings.Default.Save();

// 또는 설정 UI 제공 (SettingsCommand.cs)
```

**저장 위치**: `%AppData%\[회사명]\DXrevit.exe_[해시]\[버전]\user.config`

### 2. 주요 기능

#### 2-1. 스냅샷 생성

**사용 방법**:
1. Revit에서 프로젝트 열기
2. DX 탭 → "스냅샷" 버튼 클릭
3. 스냅샷 창에서 정보 입력:
   - 작성자
   - 설명
   - 서버 URL 확인
4. "스냅샷 생성" 버튼 클릭

**추출되는 데이터**:

```json
{
  "metadata": {
    "model_version": "프로젝트이름_20251018_120000",
    "timestamp": "2025-10-18T12:00:00Z",
    "project_name": "프로젝트 이름",
    "created_by": "yoon",
    "description": "초기 스냅샷",
    "total_object_count": 852,
    "revit_file_path": "C:\\...\\배관테스트.rvt"
  },
  "objects": [
    {
      "object_id": "e3e052f9-0156-11d5-9301-0000863f27ad-00000017",
      "element_id": 23,
      "category": "재료",
      "family": "기본값",
      "type": "Unknown",
      "activity_id": null,
      "properties": {
        "URL": "",
        "빛": 0,
        "단가": 0,
        "마크": "",
        "모델": ""
      },
      "bounding_box": null
    }
  ],
  "relationships": [
    {
      "source_object_id": "wall-001",
      "target_object_id": "door-001",
      "relation_type": "HostedBy",
      "is_directed": true
    }
  ]
}
```

**데이터 흐름**:
```
Revit Document
  ↓ FilteredElementCollector
Elements (852개)
  ↓ DataExtractor.ExtractAll()
ExtractedData (JSON)
  ↓ ApiDataWriter.SendToApi()
HTTP POST → FastAPI
  ↓
PostgreSQL (metadata, objects, relationships)
```

#### 2-2. 설정 관리

**사용 방법**:
1. DX 탭 → "설정" 버튼 클릭
2. 서버 URL, 타임아웃 등 수정
3. "저장" 버튼 클릭

**설정 가능한 항목**:
- `SERVER_URL`: FastAPI 서버 주소
- `API_VERSION`: API 버전 (v1)
- `TIMEOUT_SECONDS`: HTTP 요청 타임아웃

#### 2-3. 파라미터 설정 (v1.0에서 사용 가능하지만 미구현)

**개념**: Revit 패밀리에 Activity ID 파라미터 추가

**구현 필요 사항**:
```csharp
// DXrevit/Commands/ParameterSetupCommand.cs
public Result Execute(ExternalCommandData commandData, ...)
{
    // 1. 공유 파라미터 파일 생성/로드
    // 2. "Activity_ID" 파라미터 정의
    // 3. 모든 카테고리에 파라미터 바인딩
    // 4. 사용자에게 완료 메시지
}
```

**사용 시나리오** (v2.0):
1. 파라미터 설정 명령 실행
2. CSV 스케줄에서 Activity ID 읽기
3. Element ID 매칭하여 파라미터 자동 할당
4. 스냅샷 생성 시 Activity ID 포함

### 3. 유지보수

#### 3-1. 로그 확인

**로그 위치**: `%AppData%\DXrevit\Logs\`

```
DXrevit_20251018.log
DXrevit_20251017.log
...
```

**로그 레벨**:
```csharp
// DXBase/Services/LoggingService.cs
public static void LogInfo(string message, string source)
public static void LogWarning(string message, string source)
public static void LogError(string message, Exception ex, string source)
```

**로그 예시**:
```
2025-10-18 12:00:00 [INFO] [DXrevit] 데이터 추출 시작
2025-10-18 12:00:01 [INFO] [DXrevit] 총 852개 객체 추출 시작
2025-10-18 12:00:15 [INFO] [DXrevit] 데이터 추출 완료: 852개 객체, 70개 관계
2025-10-18 12:00:16 [INFO] [DXrevit] API 전송 시작: http://localhost:8000/api/v1/snapshot
2025-10-18 12:00:17 [INFO] [DXrevit] API 전송 성공
```

#### 3-2. 일반적인 문제 해결

**문제 1: 플러그인이 Revit에 표시되지 않음**

```bash
# 해결 방법:
# 1. 설치 경로 확인
dir "%AppData%\Autodesk\Revit\Addins\2025\"

# 2. .addin 파일 내용 확인 (XML 형식 오류 확인)
notepad "%AppData%\Autodesk\Revit\Addins\2025\DXrevit.addin"

# 3. DLL 파일 존재 확인
dir "%AppData%\Autodesk\Revit\Addins\2025\DXrevit.dll"

# 4. Revit 재시작
```

**문제 2: API 전송 실패**

```bash
# 원인: FastAPI 서버 미실행
# 해결:
cd "개발폴더\fastapi_server"
uvicorn main:app --reload

# 확인:
curl http://localhost:8000/api/v1/system/health
```

**문제 3: .NET 런타임 오류**

```bash
# 원인: .NET 8.0 미설치
# 해결:
# https://dotnet.microsoft.com/download/dotnet/8.0
# .NET Desktop Runtime 8.0 설치
```

#### 3-3. 디버깅

**Visual Studio에서 디버깅**:

```xml
<!-- DXrevit.csproj - Debug 설정 -->
<PropertyGroup Condition="'$(Configuration)'=='Debug'">
  <StartAction>Program</StartAction>
  <StartProgram>C:\Program Files\Autodesk\Revit 2025\Revit.exe</StartProgram>
</PropertyGroup>
```

**사용 방법**:
1. Visual Studio에서 F5 (디버깅 시작)
2. Revit 자동 실행
3. 중단점(Breakpoint) 설정
4. 플러그인 실행 → 코드 단계별 실행

#### 3-4. 업데이트

**버전 업그레이드 절차**:

```bash
# 1. 소스 코드 업데이트 (Git Pull)
cd "개발폴더\DXrevit"
git pull origin main

# 2. 빌드
dotnet build -c Release

# 3. 기존 플러그인 백업
xcopy "%AppData%\Autodesk\Revit\Addins\2025\DXrevit.*" ^
      "%AppData%\Autodesk\Revit\Addins\2025\backup\" /Y

# 4. 새 버전 설치
xcopy "bin\Release\net8.0-windows\*.*" ^
      "%AppData%\Autodesk\Revit\Addins\2025\" /Y

# 5. Revit 재시작
```

### 4. 고급 기능 (사용 가능하지만 v1.0에서 비활성화)

#### 4-1. 증분 업데이트

**개념**: 전체 모델이 아닌 변경된 Element만 전송

**구현 방법** (v2.0 예정):
```csharp
// 마지막 스냅샷 타임스탬프 저장
// 변경 감지: Element.GetModifiedTime() 비교
// 변경된 Element만 추출 및 전송
```

#### 4-2. 선택적 카테고리 필터링

**사용 시나리오**: 특정 카테고리만 추출 (예: 벽, 기둥만)

```csharp
// SnapshotViewModel.cs
public List<string> SelectedCategories { get; set; }

// DataExtractor.cs
var collector = new FilteredElementCollector(document)
    .WhereElementIsNotElementType()
    .Where(e => SelectedCategories.Contains(e.Category.Name));
```

---

## Navisworks 플러그인 (DXnavis)

### 1. 설치 및 설정

#### 1-1. 필수 요구사항

**소프트웨어**:
- Autodesk Navisworks Manage 2025
- .NET Framework 4.8
- Visual Studio 2022 (개발 시)

**Navisworks API 참조**:
```xml
<!-- DXnavis/DXnavis.csproj -->
<ItemGroup>
  <Reference Include="Autodesk.Navisworks.Api">
    <HintPath>C:\Program Files\Autodesk\Navisworks Manage 2025\Autodesk.Navisworks.Api.dll</HintPath>
  </Reference>
  <Reference Include="Autodesk.Navisworks.Interop">
    <HintPath>C:\Program Files\Autodesk\Navisworks Manage 2025\Autodesk.Navisworks.Interop.dll</HintPath>
  </Reference>
</ItemGroup>
```

#### 1-2. 플러그인 설치

**자동 설치 (빌드 후 복사)**:

```xml
<!-- DXnavis.csproj - PostBuild 이벤트 -->
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="
    xcopy &quot;$(TargetPath)&quot; &quot;$(ProgramFiles)\Autodesk\Navisworks Manage 2025\Plugins\&quot; /Y
    xcopy &quot;$(TargetDir)*.dll&quot; &quot;$(ProgramFiles)\Autodesk\Navisworks Manage 2025\Plugins\&quot; /Y /EXCLUDE:excludelist.txt
  " />
</Target>
```

**수동 설치**:

```bash
# 관리자 권한 필요
xcopy "개발폴더\DXnavis\bin\Debug\*.*" ^
      "C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\" /Y
```

**설치 경로**:
```
C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\
├─ DXnavis.dll
└─ 기타 의존성 DLL (DXBase.dll 등)
```

#### 1-3. 플러그인 등록

**자동 등록**: DLL을 Plugins 폴더에 복사하면 자동 등록

**확인 방법**:
1. Navisworks 실행
2. Application Menu → Options → Interface → Workspace
3. "DX" 탭 확인

#### 1-4. 설정 파일 (없음, v1.0)

**v1.0 한계**: 하드코딩된 경로 사용

```csharp
// DXnavis/Services/HierarchyFileWriter.cs
private string _outputDirectory = @"C:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더";
```

**v2.0 개선 필요**: 설정 파일 추가
```xml
<!-- DXnavis.config (예정) -->
<configuration>
  <appSettings>
    <add key="OutputDirectory" value="%USERPROFILE%\Desktop\DXnavis_Output" />
    <add key="ServerUrl" value="http://localhost:8000" />
  </appSettings>
</configuration>
```

### 2. 주요 기능

#### 2-1. 계층 정보 추출

**사용 방법**:
1. Navisworks에서 모델 열기 (.nwc, .nwf, .nwd)
2. DX 패널에서 "모델 선택" 또는 "전체 모델"
3. "계층 추출" 버튼 클릭
4. CSV 파일 자동 생성

**출력 파일**:
```
개발폴더/navis_Hierarchy_YYYYMMDD_HHMMSS.csv
개발폴더/navis_Hierarchy_YYYYMMDD_HHMMSS.xlsx (선택사항)
```

**CSV 형식**:
```csv
ObjectId,ParentId,Level,DisplayName,Category,PropertyName,PropertyValue
00000000-0000-0000-0000-000000000000,00000000-0000-0000-0000-000000000000,0,배관테스트_4D.nwc,항목,이름,DisplayString:배관테스트_4D.nwc
049dab74-be6f-4a10-906d-ca7a027aa210,00000000-0000-0000-0000-000000000000,5,Flex Pipe Round,항목,GUID,DisplayString:049dab74-be6f-4a10-906d-ca7a027aa210
```

**추출되는 속성**:
- **ObjectId**: Navisworks InstanceGuid
- **ParentId**: 부모 객체 GUID
- **Level**: 계층 깊이 (0부터 시작)
- **DisplayName**: 표시 이름
- **Category**: 속성 카테고리 (항목, Project, Type, Element ID 등)
- **PropertyName**: 속성 이름
- **PropertyValue**: 속성 값 (타입 포함)

**주요 속성 카테고리**:

| 카테고리 | 설명 | 주요 속성 |
|---------|------|----------|
| **항목** | 기본 정보 | 이름, 유형, GUID, 소스 파일 |
| **Project** | 프로젝트 정보 | Project Name, Client Name, Address |
| **Location** | 위치 정보 | Latitude, Longitude, Elevation |
| **Type** | 타입 정보 | Name, Category, Id |
| **Element ID** | Revit Element ID | 값 (Revit 연동 키) |

#### 2-2. 속성 검색 및 필터링

**사용 방법**:
1. 계층 추출 후 속성 목록 표시
2. 검색창에서 속성명 또는 값 검색
3. 체크박스 선택 → SearchSet 생성

**SearchSet 생성**:
- 선택한 객체들을 Navisworks SearchSet으로 저장
- Selection Tree에 자동 추가
- 4D 시뮬레이션, 충돌 감지 등에 활용

#### 2-3. 전체 모델 내보내기

**기능**: 모든 ModelItem을 순회하여 속성 추출

**사용 시나리오**:
- 전체 모델 데이터 백업
- 외부 분석 도구로 전송
- 데이터 마이그레이션

**주의사항**:
- 대용량 모델의 경우 시간 소요 (수분~수십분)
- 메모리 사용량 증가
- 진행률 표시 확인 필요

### 3. 유지보수

#### 3-1. 로그 확인

**로그 위치**: Visual Studio Output 창 (Debug.WriteLine)

**v1.0 한계**: 파일 로그 없음

**로그 예시**:
```
[ID 검증] Level=5, ParentId=00000000-0000-0000-0000-000000000000, CurrentId=049dab74-be6f-4a10-906d-ca7a027aa210, IsEmpty=False
[계층 추출] 총 4317개 레코드 추출 완료
[파일 저장] C:\Users\...\navis_Hierarchy_20251018_205342.csv
```

**v2.0 개선**: 파일 로그 추가 필요
```csharp
// LoggingService 통합
LoggingService.LogInfo("계층 추출 시작", "DXnavis");
```

#### 3-2. 일반적인 문제 해결

**문제 1: 플러그인이 Navisworks에 표시되지 않음**

```bash
# 해결 방법:
# 1. 설치 경로 확인 (관리자 권한 필요)
dir "C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\"

# 2. DLL 파일 존재 확인
dir "C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\DXnavis.dll"

# 3. .NET Framework 4.8 설치 확인
# Control Panel → Programs → .NET Framework 4.8

# 4. Navisworks 재시작 (관리자 권한)
```

**문제 2: CSV 파일이 생성되지 않음**

```bash
# 원인 1: 출력 경로 권한 없음
# 해결: 출력 디렉토리 권한 확인
icacls "C:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더"

# 원인 2: 경로에 한글/특수문자
# 해결: 코드에서 UTF-8 인코딩 사용 확인
```

**문제 3: AccessViolationException**

```csharp
// 원인: Navisworks API 내부 오류
// 해결: Try-Catch로 보호됨 (HierarchyFileWriter.cs)
try
{
    properties = category.Properties;
}
catch (System.AccessViolationException)
{
    Debug.WriteLine($"AccessViolationException in category: {category.DisplayName}");
    continue;
}
```

#### 3-3. 디버깅

**Visual Studio 디버깅**:

```xml
<!-- DXnavis.csproj - Debug 설정 -->
<PropertyGroup Condition="'$(Configuration)'=='Debug'">
  <StartAction>Program</StartAction>
  <StartProgram>C:\Program Files\Autodesk\Navisworks Manage 2025\Roamer.exe</StartProgram>
</PropertyGroup>
```

**사용 방법**:
1. Visual Studio에서 F5
2. Navisworks 자동 실행
3. 모델 열기
4. DX 패널 → 계층 추출
5. 중단점에서 변수 확인

#### 3-4. 업데이트

```bash
# 1. 소스 코드 업데이트
cd "개발폴더\DXnavis"
git pull origin main

# 2. 빌드
dotnet build -c Debug

# 3. 기존 플러그인 백업 (관리자 권한)
xcopy "C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\DXnavis.*" ^
      "C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\backup\" /Y

# 4. 새 버전 설치 (관리자 권한)
xcopy "bin\Debug\*.*" ^
      "C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\" /Y

# 5. Navisworks 재시작
```

### 4. 고급 기능 (v1.0에서 사용 가능)

#### 4-1. SearchSet 생성

**기능**: 선택한 객체를 Navisworks SearchSet으로 저장

**사용 시나리오**:
1. 특정 속성 조건으로 필터링
2. 체크박스 선택
3. "SearchSet 생성" 버튼
4. Selection Tree에 추가됨

**활용**:
- TimeLiner에서 작업 연결
- Clash Detective에서 충돌 감지
- Animator에서 뷰포인트 생성

#### 4-2. Excel 내보내기

**기능**: CSV와 함께 XLSX 파일 생성

**장점**:
- Excel에서 직접 열기 가능
- 필터링, 정렬 용이
- 피벗 테이블 생성 가능

**파일 형식**: `navis_Hierarchy_YYYYMMDD_HHMMSS.xlsx`

---

## FastAPI 서버

### 1. 설치 및 설정

#### 1-1. 필수 요구사항

**소프트웨어**:
- Python 3.10 이상
- pip (Python 패키지 관리자)
- PostgreSQL 15+

**의존성 패키지**:

```txt
# fastapi_server/requirements.txt
fastapi==0.104.1
uvicorn[standard]==0.24.0
asyncpg==0.29.0
pydantic==2.5.0
pydantic-settings==2.1.0
python-dotenv==1.0.0
```

#### 1-2. 설치

```bash
# 1. 가상환경 생성 (권장)
cd "개발폴더\fastapi_server"
python -m venv venv

# 2. 가상환경 활성화
# Windows
venv\Scripts\activate
# Linux/Mac
source venv/bin/activate

# 3. 의존성 설치
pip install -r requirements.txt

# 4. 환경변수 파일 생성
copy .env.example .env
```

#### 1-3. 환경변수 설정

**파일**: `fastapi_server/.env`

```bash
# 데이터베이스 연결
DATABASE_URL=postgresql://postgres:123456@localhost:5432/DX_platform
DB_POOL_MIN=1
DB_POOL_MAX=10

# 서버 설정
HOST=0.0.0.0
PORT=8000
DEBUG=True
LOG_LEVEL=INFO

# CORS 설정
ALLOWED_ORIGINS=http://localhost,http://127.0.0.1
ALLOWED_HOSTS=*
```

**설정 설명**:

| 변수 | 설명 | 기본값 | 예시 |
|------|------|--------|------|
| `DATABASE_URL` | PostgreSQL 연결 문자열 | - | `postgresql://user:pass@host:port/db` |
| `DB_POOL_MIN` | 최소 연결 풀 크기 | 1 | 1 |
| `DB_POOL_MAX` | 최대 연결 풀 크기 | 10 | 20 (고부하 시) |
| `HOST` | 서버 바인딩 주소 | 0.0.0.0 | localhost (로컬만) |
| `PORT` | 서버 포트 | 8000 | 8080 |
| `DEBUG` | 디버그 모드 | True | False (프로덕션) |
| `LOG_LEVEL` | 로그 레벨 | INFO | DEBUG, WARNING, ERROR |
| `ALLOWED_ORIGINS` | CORS 허용 출처 | * | http://localhost:3000 |

### 2. 서버 실행

#### 2-1. 개발 모드 (Hot Reload)

```bash
cd "개발폴더\fastapi_server"

# 방법 1: uvicorn 직접 실행
uvicorn main:app --reload --host 0.0.0.0 --port 8000

# 방법 2: Python 모듈로 실행
python -m uvicorn main:app --reload

# 방법 3: 환경변수 파일 지정
uvicorn main:app --reload --env-file .env
```

**출력**:
```
INFO:     Uvicorn running on http://0.0.0.0:8000 (Press CTRL+C to quit)
INFO:     Started reloader process [12345] using WatchFiles
INFO:     Started server process [12346]
INFO:     Waiting for application startup.
INFO:     Async DB pool initialized (attempt 1)
INFO:     Application startup complete.
```

#### 2-2. 프로덕션 모드

```bash
# Gunicorn + Uvicorn Workers (Linux/Mac)
gunicorn main:app --workers 4 --worker-class uvicorn.workers.UvicornWorker --bind 0.0.0.0:8000

# Windows에서는 uvicorn만 사용
uvicorn main:app --host 0.0.0.0 --port 8000 --workers 4
```

#### 2-3. 백그라운드 실행

**Windows (작업 스케줄러)**:

```powershell
# 1. 배치 파일 생성
@echo off
cd "C:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\fastapi_server"
call venv\Scripts\activate
uvicorn main:app --host 0.0.0.0 --port 8000
```

**Linux/Mac (systemd)**:

```ini
# /etc/systemd/system/dxplatform-api.service
[Unit]
Description=DX Platform FastAPI Server
After=network.target postgresql.service

[Service]
Type=simple
User=dxplatform
WorkingDirectory=/home/dxplatform/fastapi_server
Environment="PATH=/home/dxplatform/fastapi_server/venv/bin"
ExecStart=/home/dxplatform/fastapi_server/venv/bin/uvicorn main:app --host 0.0.0.0 --port 8000

[Install]
WantedBy=multi-user.target
```

```bash
# 시작
sudo systemctl start dxplatform-api

# 부팅 시 자동 시작
sudo systemctl enable dxplatform-api

# 상태 확인
sudo systemctl status dxplatform-api
```

### 3. API 엔드포인트

#### 3-1. 시스템 상태

**GET /api/v1/system/health**

```bash
curl http://localhost:8000/api/v1/system/health
```

**응답**:
```json
{
  "status": "healthy",
  "database": {
    "connected": true,
    "last_error": null
  },
  "timestamp": "2025-10-18T12:00:00Z"
}
```

#### 3-2. 스냅샷 업로드

**POST /api/v1/snapshot**

```bash
curl -X POST http://localhost:8000/api/v1/snapshot \
  -H "Content-Type: application/json" \
  -d @snapshot_data.json
```

**요청 본문**: (DXrevit에서 전송)
```json
{
  "metadata": {...},
  "objects": [...],
  "relationships": [...]
}
```

**응답**:
```json
{
  "message": "스냅샷이 성공적으로 저장되었습니다.",
  "metadata_id": 1,
  "objects_inserted": 852,
  "relationships_inserted": 70
}
```

#### 3-3. API 문서

**Swagger UI**: http://localhost:8000/docs

**ReDoc**: http://localhost:8000/redoc

**OpenAPI 스키마**: http://localhost:8000/openapi.json

### 4. 유지보수

#### 4-1. 로그 확인

**콘솔 로그**:
```
INFO:     127.0.0.1:52342 - "POST /api/v1/snapshot HTTP/1.1" 200 OK
INFO:     [snapshot] 스냅샷 저장 시작
INFO:     [snapshot] 메타데이터 저장 완료: ID=1
INFO:     [snapshot] 852개 객체 저장 완료
INFO:     [snapshot] 70개 관계 저장 완료
```

**파일 로그** (v2.0 추가 필요):
```python
# fastapi_server/logging_config.py
import logging

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("logs/dxplatform_api.log"),
        logging.StreamHandler()
    ]
)
```

#### 4-2. 데이터베이스 연결 모니터링

```python
# fastapi_server/routers/system.py
@router.get("/db/status")
async def get_db_status():
    return db.connection_status()
```

**응답**:
```json
{
  "connected": true,
  "pool_size": 5,
  "idle_connections": 3,
  "active_connections": 2,
  "last_error": null
}
```

#### 4-3. 일반적인 문제 해결

**문제 1: 서버 시작 실패 - 포트 충돌**

```bash
# 원인: 8000 포트 이미 사용 중
# 확인:
netstat -ano | findstr :8000

# 해결 1: 프로세스 종료
taskkill /PID <PID> /F

# 해결 2: 다른 포트 사용
uvicorn main:app --port 8080
```

**문제 2: 데이터베이스 연결 실패**

```bash
# 원인: PostgreSQL 미실행 또는 자격증명 오류
# 확인:
psql -h localhost -U postgres -d DX_platform

# 해결 1: PostgreSQL 시작
net start postgresql-x64-15  # Windows
sudo systemctl start postgresql  # Linux

# 해결 2: .env 파일 확인
cat .env
# DATABASE_URL 확인
```

**문제 3: CORS 오류**

```python
# fastapi_server/middleware/cors_middleware.py
# ALLOWED_ORIGINS에 클라이언트 주소 추가

# .env
ALLOWED_ORIGINS=http://localhost,http://192.168.1.100
```

#### 4-4. 성능 최적화

**연결 풀 크기 조정**:

```bash
# .env
DB_POOL_MIN=5
DB_POOL_MAX=20
```

**Worker 수 증가** (CPU 코어 수에 따라):

```bash
# 권장: (2 * CPU 코어 수) + 1
uvicorn main:app --workers 9  # 4코어 CPU
```

**캐싱 추가** (v2.0):

```python
# Redis 캐싱
from fastapi_cache import FastAPICache
from fastapi_cache.backends.redis import RedisBackend

@app.on_event("startup")
async def startup():
    redis = aioredis.from_url("redis://localhost")
    FastAPICache.init(RedisBackend(redis), prefix="dxplatform:")
```

### 5. 보안

#### 5-1. 환경변수 암호화 (v2.0)

```bash
# .env 파일 권한 제한
chmod 600 .env  # Linux/Mac
icacls .env /inheritance:r /grant:r "%USERNAME%:F"  # Windows
```

#### 5-2. HTTPS 설정 (v2.0)

```bash
# SSL 인증서 생성
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365

# uvicorn HTTPS 실행
uvicorn main:app --ssl-keyfile key.pem --ssl-certfile cert.pem
```

#### 5-3. 인증 추가 (v2.0)

```python
# JWT 토큰 기반 인증
from fastapi.security import HTTPBearer

security = HTTPBearer()

@app.post("/api/v1/snapshot")
async def create_snapshot(
    data: SnapshotData,
    token: str = Depends(security)
):
    # 토큰 검증
    # ...
```

---

## PostgreSQL 데이터베이스

### 1. 설치 및 설정

#### 1-1. 설치

**Windows**:

```bash
# 다운로드: https://www.postgresql.org/download/windows/
# PostgreSQL 15.x Installer 실행
# 설치 경로: C:\Program Files\PostgreSQL\15
# 포트: 5432
# 비밀번호 설정: 123456 (개발용, 프로덕션에서는 강력한 비밀번호 사용)
```

**Linux (Ubuntu/Debian)**:

```bash
sudo apt update
sudo apt install postgresql postgresql-contrib
sudo systemctl start postgresql
sudo systemctl enable postgresql
```

#### 1-2. 데이터베이스 생성

```bash
# 1. psql 접속
psql -U postgres

# 2. 데이터베이스 생성
CREATE DATABASE DX_platform;

# 3. 사용자 생성 (선택사항)
CREATE USER dxplatform WITH ENCRYPTED PASSWORD 'your_password';
GRANT ALL PRIVILEGES ON DATABASE DX_platform TO dxplatform;

# 4. 연결 확인
\c DX_platform
\dt  # 테이블 목록 (비어있음)
```

#### 1-3. 테이블 스키마 생성

```bash
# 스크립트 실행
psql -U postgres -d DX_platform -f database/init/001_create_tables.sql
```

**스크립트 내용**: `database/init/001_create_tables.sql`

```sql
-- metadata 테이블
CREATE TABLE IF NOT EXISTS metadata (
    model_version VARCHAR(255) PRIMARY KEY,
    timestamp TIMESTAMP WITH TIME ZONE NOT NULL,
    project_name VARCHAR(255) NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    description TEXT,
    total_object_count INTEGER DEFAULT 0,
    revit_file_path TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- objects 테이블
CREATE TABLE IF NOT EXISTS objects (
    id BIGSERIAL PRIMARY KEY,
    model_version VARCHAR(255) NOT NULL,
    object_id VARCHAR(255) NOT NULL,
    element_id INTEGER NOT NULL,
    category VARCHAR(255) NOT NULL,
    family VARCHAR(255),
    type VARCHAR(255),
    activity_id VARCHAR(100),
    properties JSONB NOT NULL DEFAULT '{}'::jsonb,
    bounding_box JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- relationships 테이블
CREATE TABLE IF NOT EXISTS relationships (
    id BIGSERIAL PRIMARY KEY,
    model_version VARCHAR(255) NOT NULL,
    source_object_id VARCHAR(255) NOT NULL,
    target_object_id VARCHAR(255) NOT NULL,
    relation_type VARCHAR(50) NOT NULL,
    is_directed BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- 인덱스 생성
CREATE INDEX IF NOT EXISTS idx_objects_model_version ON objects(model_version);
CREATE INDEX IF NOT EXISTS idx_objects_category ON objects(category);
CREATE INDEX IF NOT EXISTS idx_objects_element_id ON objects(element_id);
CREATE INDEX IF NOT EXISTS idx_relationships_model_version ON relationships(model_version);
```

### 2. 데이터 관리

#### 2-1. 데이터 조회

**전체 프로젝트 목록**:

```sql
SELECT
    project_name,
    model_version,
    total_object_count,
    created_at
FROM metadata
ORDER BY created_at DESC;
```

**특정 프로젝트 객체**:

```sql
SELECT
    category,
    COUNT(*) as count
FROM objects
WHERE model_version = '프로젝트 이름_20251016_030006'
GROUP BY category
ORDER BY count DESC;
```

**속성 검색 (JSONB)**:

```sql
-- 특정 속성값 검색
SELECT
    object_id,
    category,
    properties->>'이름' AS name,
    properties->>'유형' AS type
FROM objects
WHERE properties->>'단가' IS NOT NULL
  AND (properties->>'단가')::NUMERIC > 0;

-- JSONB 인덱스 활용
CREATE INDEX idx_objects_properties ON objects USING GIN(properties);
```

#### 2-2. 데이터 삭제

**프로젝트 전체 삭제**:

```sql
BEGIN;

DELETE FROM relationships WHERE model_version LIKE '프로젝트 이름%';
DELETE FROM objects WHERE model_version LIKE '프로젝트 이름%';
DELETE FROM metadata WHERE project_name = '프로젝트 이름';

COMMIT;
```

**스크립트 사용**:

```bash
# scripts/delete_project.sql 실행
psql -U postgres -d DX_platform -v project_name="'프로젝트 이름'" -f scripts/delete_project.sql
```

#### 2-3. 백업 및 복구

**전체 백업**:

```bash
# 데이터베이스 전체 백업
pg_dump -h localhost -U postgres -d DX_platform > backup_$(date +%Y%m%d).sql

# 특정 테이블만 백업
pg_dump -h localhost -U postgres -d DX_platform -t objects > objects_backup.sql

# 압축 백업
pg_dump -h localhost -U postgres -d DX_platform | gzip > backup.sql.gz
```

**복구**:

```bash
# 전체 복구
psql -U postgres -d DX_platform < backup_20251018.sql

# 압축 파일 복구
gunzip -c backup.sql.gz | psql -U postgres -d DX_platform
```

**자동 백업 (cron)**:

```bash
# crontab -e
# 매일 새벽 2시 백업
0 2 * * * pg_dump -U postgres DX_platform | gzip > /backup/dx_platform_$(date +\%Y\%m\%d).sql.gz
```

### 3. 유지보수

#### 3-1. 성능 모니터링

**활성 쿼리 확인**:

```sql
SELECT
    pid,
    usename,
    application_name,
    client_addr,
    state,
    query,
    query_start
FROM pg_stat_activity
WHERE state != 'idle'
ORDER BY query_start;
```

**느린 쿼리 찾기**:

```sql
SELECT
    query,
    calls,
    total_exec_time,
    mean_exec_time,
    max_exec_time
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 10;
```

**테이블 크기 확인**:

```sql
SELECT
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size,
    pg_total_relation_size(schemaname||'.'||tablename) AS bytes
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY bytes DESC;
```

#### 3-2. 인덱스 최적화

**인덱스 사용 통계**:

```sql
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan,
    idx_tup_read,
    idx_tup_fetch
FROM pg_stat_user_indexes
WHERE idx_scan = 0  -- 사용되지 않는 인덱스
ORDER BY schemaname, tablename;
```

**누락된 인덱스 찾기**:

```sql
-- 자주 조회되지만 인덱스 없는 컬럼
SELECT
    schemaname,
    tablename,
    attname
FROM pg_stats
WHERE schemaname = 'public'
  AND n_distinct < -0.01  -- 높은 카디널리티
ORDER BY tablename, attname;
```

#### 3-3. VACUUM 및 ANALYZE

**수동 실행**:

```sql
-- VACUUM: 삭제된 행 정리, 공간 회수
VACUUM FULL objects;

-- ANALYZE: 통계 정보 업데이트
ANALYZE objects;

-- 동시 실행
VACUUM ANALYZE objects;
```

**자동 VACUUM 설정**:

```sql
-- postgresql.conf
autovacuum = on
autovacuum_max_workers = 3
autovacuum_naptime = 1min
```

#### 3-4. 연결 관리

**최대 연결 수 설정**:

```sql
-- postgresql.conf
max_connections = 100

-- 재시작 필요
sudo systemctl restart postgresql
```

**연결 풀 사용** (애플리케이션 레벨):

```python
# FastAPI에서 이미 구현됨
# database.py - asyncpg.create_pool()
# min_size=1, max_size=10
```

### 4. 보안

#### 4-1. 비밀번호 변경

```sql
ALTER USER postgres WITH PASSWORD 'new_strong_password';
```

#### 4-2. pg_hba.conf 설정

**위치**: `C:\Program Files\PostgreSQL\15\data\pg_hba.conf`

```conf
# TYPE  DATABASE        USER            ADDRESS                 METHOD

# Local connections
local   all             postgres                                md5
host    all             postgres        127.0.0.1/32            md5
host    all             postgres        ::1/128                 md5

# Remote connections (주의: 프로덕션에서는 특정 IP만 허용)
# host    all             all             0.0.0.0/0               md5
```

**변경 후 재시작**:

```bash
sudo systemctl restart postgresql  # Linux
net stop postgresql-x64-15 && net start postgresql-x64-15  # Windows
```

#### 4-3. SSL 연결

```sql
-- postgresql.conf
ssl = on
ssl_cert_file = 'server.crt'
ssl_key_file = 'server.key'
```

**클라이언트 연결**:

```bash
psql "postgresql://postgres@localhost/DX_platform?sslmode=require"
```

### 5. 고급 기능

#### 5-1. 파티셔닝 (v2.0)

**개념**: 대용량 테이블을 작은 파티션으로 분할

```sql
-- 프로젝트별 파티셔닝
CREATE TABLE objects_partitioned (
    LIKE objects INCLUDING ALL
) PARTITION BY LIST (model_version);

CREATE TABLE objects_project1 PARTITION OF objects_partitioned
FOR VALUES IN ('프로젝트1_20251016_030006');

CREATE TABLE objects_project2 PARTITION OF objects_partitioned
FOR VALUES IN ('프로젝트2_20251017_170052');
```

#### 5-2. 복제 (Replication)

**Streaming Replication**: Master-Slave 구조

```sql
-- Master 설정
# postgresql.conf
wal_level = replica
max_wal_senders = 3
```

#### 5-3. 외부 데이터 래퍼 (FDW)

**다른 데이터베이스 연결**:

```sql
-- postgres_fdw 설치
CREATE EXTENSION postgres_fdw;

-- 외부 서버 정의
CREATE SERVER remote_db
FOREIGN DATA WRAPPER postgres_fdw
OPTIONS (host 'remote-host', dbname 'remote_db', port '5432');

-- 사용자 매핑
CREATE USER MAPPING FOR postgres
SERVER remote_db
OPTIONS (user 'remote_user', password 'remote_pass');

-- 외부 테이블 생성
CREATE FOREIGN TABLE remote_objects (...)
SERVER remote_db
OPTIONS (schema_name 'public', table_name 'objects');
```

---

## 상호작용 워크플로우

### 워크플로우 1: Revit → SQL (기본 스냅샷)

```
1. Revit에서 프로젝트 열기
   ↓
2. DX 탭 → 스냅샷 버튼 클릭
   ↓
3. 스냅샷 정보 입력
   - 작성자: yoon
   - 설명: 초기 스냅샷
   - 서버 URL: http://localhost:8000 (자동)
   ↓
4. "스냅샷 생성" 버튼
   ↓
5. DXrevit: 데이터 추출
   - FilteredElementCollector
   - 852개 Element
   - Properties → JSONB
   ↓
6. DXrevit: API 전송
   - POST /api/v1/snapshot
   - JSON 본문
   ↓
7. FastAPI: 수신 및 검증
   - Pydantic 모델 검증
   ↓
8. FastAPI: PostgreSQL 저장
   - metadata 테이블: 1개 행
   - objects 테이블: 852개 행
   - relationships 테이블: 70개 행
   ↓
9. 성공 메시지 표시
   "스냅샷이 성공적으로 저장되었습니다."
```

### 워크플로우 2: Navisworks → CSV (계층 추출)

```
1. Navisworks에서 모델 열기
   - 배관테스트.rvt → 배관테스트_4D.nwc 변환
   ↓
2. DX 패널 표시 확인
   ↓
3. "전체 모델" 선택
   ↓
4. "계층 추출" 버튼 클릭
   ↓
5. DXnavis: 계층 순회
   - TraverseAndExtractProperties()
   - 재귀적 트리 탐색
   - 4,317개 레코드 수집
   ↓
6. DXnavis: CSV 저장
   - 파일명: navis_Hierarchy_20251018_205342.csv
   - 위치: 개발폴더/
   ↓
7. 완료 메시지 표시
   "계층 정보 추출 완료"
   ↓
8. (v1.0 수동) CSV → SQL 업로드
   - scripts/import_hierarchy_csv.py 실행
   - 또는 Power BI/Excel에서 직접 읽기
```

### 워크플로우 3: SQL 데이터 분석 (Power BI)

```
1. Power BI Desktop 실행
   ↓
2. 데이터 가져오기 → PostgreSQL
   ↓
3. 연결 정보 입력
   - 서버: localhost
   - 데이터베이스: DX_platform
   - 사용자: postgres
   - 비밀번호: 123456
   ↓
4. 테이블 선택
   - metadata
   - objects
   - relationships
   ↓
5. 데이터 변환 (Power Query)
   - JSONB properties 열 확장
   - 날짜 형식 변환
   ↓
6. 관계 설정
   - metadata.model_version → objects.model_version
   ↓
7. 시각화 생성
   - 카테고리별 객체 수 (막대 차트)
   - 프로젝트 타임라인 (간트 차트)
   - 속성 분포 (히트맵)
   ↓
8. 대시보드 게시
```

### 워크플로우 4: 리비전 비교 (v2.0 예정)

```
1. 동일 프로젝트에서 두 번째 스냅샷
   ↓
2. FastAPI: 자동 변경 감지
   - 이전 리비전과 비교
   - 추가/수정/삭제 분류
   ↓
3. SQL: change_type 업데이트
   - objects.change_type = 'added' | 'modified' | 'deleted'
   ↓
4. Power BI: 변경사항 대시보드
   - 추가된 객체: 10개
   - 수정된 객체: 5개
   - 삭제된 객체: 2개
```

---

## 트러블슈팅

### 문제 해결 체크리스트

#### 1. Revit 플러그인이 작동하지 않음

```
☐ DXrevit.dll 파일 존재 확인
  → %AppData%\Autodesk\Revit\Addins\2025\

☐ DXrevit.addin 파일 확인
  → XML 형식 오류 없는지 확인

☐ .NET 런타임 설치 확인
  → .NET 8.0 Desktop Runtime

☐ Revit 재시작

☐ 로그 확인
  → %AppData%\DXrevit\Logs\
```

#### 2. Navisworks 플러그인이 표시되지 않음

```
☐ 관리자 권한으로 설치 확인
  → C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\

☐ DXnavis.dll 파일 존재 확인

☐ .NET Framework 4.8 설치 확인

☐ Navisworks 관리자 권한으로 재시작

☐ Output 창에서 Debug 메시지 확인
```

#### 3. FastAPI 서버 연결 실패

```
☐ 서버 실행 중인지 확인
  → curl http://localhost:8000/api/v1/system/health

☐ 포트 충돌 확인
  → netstat -ano | findstr :8000

☐ 방화벽 설정 확인
  → Windows Defender → 포트 8000 허용

☐ .env 파일 존재 확인
  → DATABASE_URL 설정 확인

☐ PostgreSQL 연결 확인
  → psql -U postgres -d DX_platform
```

#### 4. PostgreSQL 데이터베이스 오류

```
☐ PostgreSQL 서비스 실행 확인
  → net start postgresql-x64-15 (Windows)
  → sudo systemctl status postgresql (Linux)

☐ 데이터베이스 존재 확인
  → psql -U postgres -l | grep DX_platform

☐ 테이블 스키마 존재 확인
  → psql -U postgres -d DX_platform -c "\dt"

☐ 연결 권한 확인
  → pg_hba.conf 설정

☐ 로그 확인
  → C:\Program Files\PostgreSQL\15\data\log\
```

### 일반적인 에러 메시지

| 에러 메시지 | 원인 | 해결 방법 |
|------------|------|----------|
| `Could not load file or assembly 'DXrevit'` | DLL 파일 누락 또는 경로 오류 | 빌드 출력 확인, PostBuild 이벤트 실행 |
| `Connection refused` | FastAPI 서버 미실행 | `uvicorn main:app --reload` 실행 |
| `relation "objects" does not exist` | 테이블 미생성 | `001_create_tables.sql` 실행 |
| `password authentication failed` | 잘못된 비밀번호 | `.env` 파일의 `DATABASE_URL` 확인 |
| `Port 8000 is already in use` | 포트 충돌 | 프로세스 종료 또는 다른 포트 사용 |

---

## v2.0 업그레이드 준비

### v2.0에서 추가될 주요 기능

#### 1. 프로젝트 및 리비전 관리

**새 테이블**:
- `projects`: 프로젝트 마스터
- `revisions`: 리비전 이력
- `unified_objects`: Revit + Navisworks 통합

**새 기능**:
- 자동 프로젝트 감지 (파일명 기반)
- 리비전 번호 자동 할당
- 변경사항 추적 (added/modified/deleted)

#### 2. Navisworks SQL 업로드

**새 엔드포인트**:
```python
POST /api/v1/navisworks/projects/{code}/revisions/{number}/hierarchy
```

**DXnavis 개선**:
- CSV 저장 후 자동 업로드
- 프로젝트 코드 자동 감지
- 진행률 표시

#### 3. 계층 정보 통합

**Revit 계층 정보 추가**:
- `parent_object_id`: 부모 객체 ID
- `level`: 계층 깊이
- `spatial_path`: Building > Level > Room

**통합 뷰**:
```sql
v_integrated_objects
v_hierarchy_tree
v_revision_changes
```

#### 4. ELT 데이터 파이프라인

**Materialized Views**:
- `v_bi_objects`: Power BI용 평면 뷰
- `v_bi_hierarchy`: 계층 구조 뷰
- `v_bi_4d_schedule`: 4D 시뮬레이션 뷰

**자동 새로고침**:
- 새 리비전 생성 시 트리거
- pg_cron으로 정기 갱신

### v2.0 마이그레이션 체크리스트

```
☐ 기존 데이터 백업
  → pg_dump -U postgres DX_platform > backup_v1.sql

☐ 새 스키마 생성
  → 002_integrated_schema.sql 실행

☐ 데이터 마이그레이션
  → scripts/migrate_to_v2.py 실행

☐ DXrevit 플러그인 업데이트
  → ProjectManager, RevisionManager 추가

☐ DXnavis 플러그인 업데이트
  → HierarchyUploader 추가

☐ FastAPI 엔드포인트 추가
  → projects, revisions, navisworks 라우터

☐ Materialized Views 생성
  → 003_bi_views.sql 실행

☐ 테스트
  → 배관테스트.rvt로 end-to-end 테스트

☐ 문서 업데이트
  → SYSTEM_MAINTENANCE_GUIDE_V2.md 작성
```

---

## 부록

### A. 주요 파일 경로

**Revit 플러그인**:
```
개발폴더/DXrevit/
├─ Application.cs                  # 플러그인 진입점
├─ Commands/
│  ├─ SnapshotCommand.cs          # 스냅샷 명령
│  ├─ SettingsCommand.cs          # 설정 명령
│  └─ ParameterSetupCommand.cs    # 파라미터 설정
├─ Services/
│  ├─ DataExtractor.cs            # 데이터 추출
│  └─ ApiDataWriter.cs            # API 전송
├─ Views/
│  ├─ SnapshotView.xaml           # 스냅샷 UI
│  └─ SettingsView.xaml           # 설정 UI
└─ Properties/
   └─ Settings.settings            # 설정 파일
```

**Navisworks 플러그인**:
```
개발폴더/DXnavis/
├─ DX.cs                           # 플러그인 진입점
├─ Services/
│  ├─ NavisworksDataExtractor.cs  # 계층 추출
│  ├─ HierarchyFileWriter.cs      # CSV 저장
│  └─ SetCreationService.cs       # SearchSet 생성
├─ Models/
│  ├─ HierarchicalPropertyRecord.cs  # 데이터 모델
│  └─ TreeNodeModel.cs            # 트리 노드
└─ Views/
   └─ DXwindow.xaml               # 메인 UI
```

**FastAPI 서버**:
```
개발폴더/fastapi_server/
├─ main.py                         # 메인 애플리케이션
├─ config.py                       # 설정 관리
├─ database.py                     # DB 연결
├─ routers/
│  └─ system.py                    # 시스템 라우터
├─ middleware/
│  ├─ cors_middleware.py          # CORS
│  └─ error_handler.py            # 오류 처리
└─ .env                            # 환경변수
```

**데이터베이스 스크립트**:
```
개발폴더/database/
├─ init/
│  └─ 001_create_tables.sql       # 초기 스키마
├─ migrations/
│  ├─ 002_integrated_schema.sql   # v2.0 스키마
│  └─ 003_bi_views.sql            # BI 뷰
└─ tables/
   └─ navisworks_hierarchy.sql    # Navisworks 테이블
```

**유틸리티 스크립트**:
```
개발폴더/scripts/
├─ query_database.py               # 데이터 조회
├─ analyze_database.py             # 데이터 분석
├─ delete_snowdon_towers.py        # 데이터 삭제
├─ import_hierarchy_csv.py         # CSV 임포트
└─ test_db_connection.py           # 연결 테스트
```

### B. 환경변수 참조

| 변수명 | 위치 | 설명 | 예시 |
|--------|------|------|------|
| `SERVER_URL` | DXrevit Settings | FastAPI 서버 주소 | `http://localhost:8000` |
| `DATABASE_URL` | fastapi_server/.env | PostgreSQL 연결 문자열 | `postgresql://user:pass@host:port/db` |
| `DB_POOL_MIN` | fastapi_server/.env | 최소 연결 풀 크기 | `1` |
| `DB_POOL_MAX` | fastapi_server/.env | 최대 연결 풀 크기 | `10` |
| `ALLOWED_ORIGINS` | fastapi_server/.env | CORS 허용 출처 | `http://localhost` |

### C. 포트 및 서비스

| 서비스 | 포트 | 프로토콜 | 용도 |
|--------|------|---------|------|
| FastAPI | 8000 | HTTP | REST API |
| PostgreSQL | 5432 | TCP | 데이터베이스 |
| Swagger UI | 8000/docs | HTTP | API 문서 |
| ReDoc | 8000/redoc | HTTP | API 문서 |

### D. 연락처 및 지원

**개발팀**:
- 이메일: support@dxplatform.com
- GitHub: https://github.com/dxplatform/awp-2025
- 문서: https://docs.dxplatform.com

**커뮤니티**:
- Discord: https://discord.gg/dxplatform
- 포럼: https://forum.dxplatform.com

---

**문서 버전**: 1.0
**최종 수정**: 2025-10-18
**다음 업데이트**: v2.0 (통합 개선 완료 후)
**작성자**: DX Platform Development Team
