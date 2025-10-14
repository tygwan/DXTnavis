# 개발 환경 설정 가이드

DX Platform 개발자를 위한 환경 설정 문서입니다.

## 필수 요구사항

### 소프트웨어
- **Visual Studio 2022** (17.8+)
  - 워크로드: .NET desktop development
  - 워크로드: Office/SharePoint development
- **.NET 8.0 SDK**
- **.NET Framework 4.8 Developer Pack**
- **Git** (2.40+)
- **PowerShell** 7.0+ (선택)

### Autodesk 제품
- **Autodesk Revit 2025** (필수)
  - 설치 경로: `C:\Program Files\Autodesk\Revit 2025\`
- **Autodesk Navisworks 2025** (선택)
  - DXnavis 개발 시 필요

## 로컬 개발 환경 구축

### 1. 저장소 클론

```bash
git clone https://github.com/tygwan/BIM-DXPlatform.git
cd BIM-DXPlatform
```

### 2. Revit API 참조 확인

`DXrevit/DXrevit.csproj` 파일에서 Revit API 경로 확인:

```xml
<Reference Include="RevitAPI">
  <HintPath>..\..\..\..\..\..\Program Files\Autodesk\Revit 2025\RevitAPI.dll</HintPath>
  <Private>False</Private>
</Reference>
```

경로가 다른 경우 수정:
```bash
# 실제 Revit 설치 경로 확인
dir "C:\Program Files\Autodesk\Revit 2025\RevitAPI.dll"
```

### 3. NuGet 패키지 복원

```bash
dotnet restore
```

### 4. 빌드

#### 전체 솔루션 빌드
```bash
dotnet build DXPlatform.sln -c Debug
```

#### 개별 프로젝트 빌드
```bash
# DXBase
dotnet build DXBase/DXBase.csproj

# DXrevit (자동 배포 포함)
dotnet build DXrevit/DXrevit.csproj
```

### 5. 배포 확인

빌드 성공 후 자동 배포 확인:
```bash
ls "C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit"
```

예상 출력:
```
DXrevit.dll
DXBase.dll
System.Text.Json.dll
System.Text.Encodings.Web.dll
System.IO.Pipelines.dll
Resources/
```

## 개발 워크플로우

### 브랜치 전략

```
main            (프로덕션 릴리스)
  ├── develop   (개발 통합 브랜치)
      ├── feature/snapshot-ui
      ├── feature/realtime-sync
      └── bugfix/parameter-binding
```

### 작업 순서

1. **Feature 브랜치 생성**
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/your-feature-name
   ```

2. **코드 작성 및 테스트**
   ```bash
   dotnet build
   # Revit에서 수동 테스트
   ```

3. **커밋**
   ```bash
   git add .
   git commit -m "feat: Add snapshot progress tracking"
   ```

4. **푸시 및 PR**
   ```bash
   git push origin feature/your-feature-name
   # GitHub에서 Pull Request 생성
   ```

## 디버깅

### Revit 디버깅 설정

Visual Studio에서 DXrevit 프로젝트 속성 → Debug:

```
Start external program: C:\Program Files\Autodesk\Revit 2025\Revit.exe
Working directory: C:\Program Files\Autodesk\Revit 2025\
```

**F5**를 누르면 Revit이 디버깅 모드로 실행됩니다.

### 로그 확인

#### 애플리케이션 로그
```bash
cat "C:\Users\$env:USERNAME\AppData\Roaming\DX_Platform\Logs\DX_$(Get-Date -Format yyyyMMdd).log"
```

#### 디버그 로그
```bash
cat "C:\Users\$env:USERNAME\Desktop\AWP_2025\개발폴더\Errorlog\DXrevit_startup.log"
```

#### Revit Journal 파일
```bash
# 최신 journal 파일 확인
ls "C:\Users\$env:USERNAME\AppData\Local\Autodesk\Revit\Autodesk Revit 2025\Journals" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
```

## 자동 배포 시스템

### PostBuild 이벤트

`DXrevit.csproj`의 `PostBuild` 타겟:

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <PropertyGroup>
    <RevitAddinsPath>C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit</RevitAddinsPath>
  </PropertyGroup>

  <!-- DLL 복사 -->
  <Copy SourceFiles="@(OutputFiles)"
        DestinationFolder="$(RevitAddinsPath)\%(RecursiveDir)"
        SkipUnchangedFiles="true" />

  <!-- .addin 파일 복사 -->
  <Copy SourceFiles="$(ProjectDir)DXrevit.addin"
        DestinationFiles="C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit.addin"
        SkipUnchangedFiles="true" />
</Target>
```

### 수동 배포

자동 배포가 실패한 경우:

```powershell
# 배포 스크립트 실행
.\scripts\deploy.ps1 -Target Revit -Configuration Debug
```

## 문제 해결

### Revit 애드인이 로드되지 않음

**증상**: Revit 실행 시 "DX Platform" 리본이 보이지 않음

**해결 단계**:

1. **DLL 존재 확인**
   ```bash
   ls "C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit"
   ```

2. **.addin 파일 확인**
   ```bash
   cat "C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit.addin"
   ```

3. **Assembly 경로 확인** (절대 경로여야 함)
   ```xml
   <Assembly>C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit\DXrevit.dll</Assembly>
   ```

4. **디버그 로그 확인**
   ```bash
   cat "C:\Users\$env:USERNAME\Desktop\AWP_2025\개발폴더\Errorlog\DXrevit_startup.log"
   ```

5. **Revit 프로세스 완전 종료**
   ```bash
   taskkill /F /IM Revit.exe
   ```

### System.Text.Json 로드 실패

**증상**: `FileNotFoundException: System.Text.Json`

**해결**: `CopyLocalLockFileAssemblies` 확인

```xml
<PropertyGroup>
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>
```

Clean → Rebuild:
```bash
dotnet clean
dotnet build
```

### 빌드 오류: Revit API 참조 실패

**증상**: `error CS0246: The type or namespace name 'Autodesk' could not be found`

**해결**: Revit API DLL 경로 확인 및 수정

```bash
# Revit 설치 확인
dir "C:\Program Files\Autodesk\Revit 2025\RevitAPI.dll"

# 없으면 경로 수정 필요
```

## 유용한 스크립트

### Clean 빌드
```bash
dotnet clean && dotnet build -c Debug
```

### 전체 재배포
```bash
Remove-Item "C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit" -Recurse -Force
dotnet build DXrevit/DXrevit.csproj
```

### 로그 초기화
```bash
Remove-Item "C:\Users\$env:USERNAME\Desktop\AWP_2025\개발폴더\Errorlog\*.log"
Remove-Item "C:\Users\$env:USERNAME\AppData\Roaming\DX_Platform\Logs\*.log"
```

## 코딩 표준

### C# 컨벤션
- **네이밍**: PascalCase (클래스, 메서드), camelCase (필드, 변수)
- **접두사**: `DX_` (공유 매개변수), `_` (private 필드)
- **비동기**: `async/await` 사용, 메서드명 `...Async` 접미사

### 주석
```csharp
/// <summary>
/// 스냅샷 데이터를 데이터베이스에 저장합니다.
/// </summary>
/// <param name="snapshot">저장할 스냅샷 데이터</param>
/// <returns>성공 시 true, 실패 시 false</returns>
public async Task<bool> SaveSnapshotAsync(SnapshotDTO snapshot)
{
    // 구현
}
```

### 오류 처리
```csharp
try
{
    // 작업
}
catch (Exception ex)
{
    LoggingService.LogError("작업 실패", "DXrevit", ex);
    TaskDialog.Show("오류", $"작업 실패: {ex.Message}");
    return Result.Failed;
}
```

## 참고 자료

- [Revit API Docs](https://www.revitapidocs.com/)
- [Navisworks API Docs](https://help.autodesk.com/view/NAV/2025/ENU/)
- [.NET 8 Docs](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [Building Coder Blog](https://thebuildingcoder.typepad.com/)

# FastAPI 서버 실행 가이드

## 사전 준비
- PostgreSQL 15+ 설치 및 데이터베이스 생성(`dx_platform`).
- 데이터베이스 초기화: `psql -U <user> -d dx_platform -f database/init_database.sql`.

## 의존성 설치 및 환경설정
```bash
cd fastapi_server
python -m venv venv
venv\\Scripts\\activate   # Windows (Mac/Linux: source venv/bin/activate)
pip install -r requirements.txt
copy .env.example .env   # 값 수정: DATABASE_URL, ALLOWED_ORIGINS, ALLOWED_HOSTS
```

## 실행
```bash
uvicorn fastapi_server.main:app --host 0.0.0.0 --port 5000 --reload
```

## 기본 점검
- `GET /health` : 서버/DB 상태
- `GET /metrics` : Prometheus 메트릭
- `POST /api/v1/ingest` : DXrevit 스냅샷 수집
- 분석 API :
  - `GET /api/v1/models/versions?projectName=...&limit=100&offset=0`
  - `GET /api/v1/models/{version}/summary`
  - `GET /api/v1/models/compare?v1=...&v2=...&changeType=ADDED&limit=1000`
  - `GET /api/v1/timeliner/{version}/mapping`

## 보안 설정
- CORS: `.env`의 `ALLOWED_ORIGINS`로 제한
- Trusted Host: `.env`의 `ALLOWED_HOSTS` 지정
- 보안 헤더: X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy, (선택) HSTS, 최소 CSP

## 권장 모듈 버전
- fastapi 0.110.x, uvicorn 0.30.x, asyncpg 0.29.x
- pydantic 2.7.x, pydantic-settings 2.2.x, prometheus-client 0.20.x
