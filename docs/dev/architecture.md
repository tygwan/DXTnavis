# DX Platform 아키텍처

시스템 전체 구조 및 설계 결정 문서입니다.

## 시스템 개요

```
┌──────────────────────────────────────────────────────────────┐
│                     DX Platform                              │
│                                                              │
│  ┌──────────────┐      ┌──────────────┐      ┌───────────┐ │
│  │   DXrevit    │────▶ │  DXserver    │────▶ │ Database  │ │
│  │  (Producer)  │      │    (API)     │      │(PostgreSQL│ │
│  └──────────────┘      └──────────────┘      └───────────┘ │
│         │                     ▲                             │
│         │                     │                             │
│         ▼                     │                             │
│  ┌──────────────┐             │                             │
│  │   DXBase     │◀────────────┘                             │
│  │  (Shared)    │                                           │
│  └──────────────┘                                           │
│         ▲                                                   │
│         │                                                   │
│         │                                                   │
│  ┌──────────────┐                                           │
│  │   DXnavis    │                                           │
│  │  (Consumer)  │                                           │
│  └──────────────┘                                           │
└──────────────────────────────────────────────────────────────┘
```

## 컴포넌트 아키텍처

### DXBase (공용 라이브러리)

**역할**: 크로스 플랫폼 공유 로직

**타겟 프레임워크**: Multi-targeting
- `.NET Standard 2.0`: Navisworks (.NET Framework 4.8) 호환
- `.NET 8.0`: Revit 2025, DXserver 호환

**주요 컴포넌트**:

```csharp
DXBase/
├── Services/
│   ├── ConfigurationService.cs   // 설정 관리
│   ├── LoggingService.cs          // 통합 로깅
│   └── ValidationService.cs       // 데이터 검증
├── Models/
│   ├── DTOs/                      // 데이터 전송 객체
│   │   ├── SnapshotDTO.cs
│   │   ├── ElementDTO.cs
│   │   └── SyncDTO.cs
│   └── Enums/
│       ├── ElementType.cs
│       └── SyncStatus.cs
└── Utils/
    ├── JsonHelper.cs              // JSON 직렬화
    └── DateTimeHelper.cs          // 시간 유틸리티
```

**설계 결정**:
- ✅ **의존성 최소화**: `System.Text.Json`만 사용
- ✅ **버전 분리**: 타겟별 다른 System.Text.Json 버전
  - `.NET 8.0`: v8.0.5
  - `.NET Standard 2.0`: v7.0.3

---

### DXrevit (Revit 애드인)

**역할**: BIM 데이터 추출 및 전송

**타겟 프레임워크**: `.NET 8.0-windows`

**아키텍처 패턴**: MVVM (Model-View-ViewModel)

```csharp
DXrevit/
├── Application.cs                 // 진입점 (IExternalApplication)
├── Commands/                      // External Commands
│   ├── SnapshotCommand.cs        // 스냅샷 저장
│   ├── ParameterSetupCommand.cs  // 매개변수 설정
│   └── SettingsCommand.cs        // 설정 UI
├── Services/
│   ├── DataExtractor.cs          // Revit 데이터 추출
│   ├── ApiClient.cs              // HTTP 통신
│   └── ParameterManager.cs       // 매개변수 관리
├── ViewModels/
│   ├── SnapshotViewModel.cs      // 스냅샷 UI 로직
│   └── SettingsViewModel.cs      // 설정 UI 로직
├── Views/
│   ├── SnapshotView.xaml         // 스냅샷 UI
│   └── SettingsView.xaml         // 설정 UI
└── Resources/
    └── SharedParameters.txt       // 공유 매개변수 정의
```

**데이터 흐름**:

```
Revit Document
    ↓ (FilteredElementCollector)
DataExtractor
    ↓ (변환)
ElementDTO[]
    ↓ (그룹화)
SnapshotDTO
    ↓ (JSON 직렬화)
ApiClient
    ↓ (HTTP POST)
DXserver
```

**설계 결정**:
- ✅ **UI 분리**: WPF XAML + ViewModel
- ✅ **비동기 처리**: `async/await` 패턴
- ✅ **오류 복구**: 재시도 로직 + 사용자 알림
- ✅ **진행률 추적**: `IProgress<T>` 인터페이스

---

### DXnavis (Navisworks 애드인)

**역할**: CSV 스케줄 → Timeliner 자동 연결

**타겟 프레임워크**: `.NET Framework 4.8`

**아키텍처 패턴**: Plugin Pattern

```csharp
DXnavis/
├── Application.cs                 // 진입점 (AddInPlugin)
├── Parsers/
│   ├── CsvParser.cs              // CSV 파싱
│   └── ScheduleMapper.cs         // 스케줄 매핑
├── Connectors/
│   ├── TimelinerConnector.cs     // Timeliner API 연결
│   └── ObjectSetMapper.cs        // ModelItem → ObjectSet
└── UI/
    └── DockPaneControl.cs        // Docking Panel
```

**상태**: Phase 4 개발 예정

---

### DXserver (API 서버)

**역할**: 중앙 데이터 수신/저장 및 동기화

**타겟 프레임워크**: `.NET 8.0` (ASP.NET Core)

**아키텍처 패턴**: Clean Architecture

```csharp
DXserver/
├── Controllers/
│   ├── SnapshotController.cs     // POST /api/snapshots
│   ├── ElementController.cs      // GET /api/elements/:id
│   └── SyncController.cs         // POST /api/sync
├── Services/
│   ├── SnapshotService.cs        // 비즈니스 로직
│   └── DiffService.cs            // 변경 비교
├── Repositories/
│   ├── ISnapshotRepository.cs    // 인터페이스
│   └── SnapshotRepository.cs     // EF Core 구현
├── Models/
│   ├── Snapshot.cs               // Entity
│   └── Element.cs                // Entity
└── Database/
    └── ApplicationDbContext.cs   // EF Core Context
```

**상태**: Phase 3 개발 예정

---

## 데이터 모델

### SnapshotDTO

```csharp
public class SnapshotDTO
{
    public string SnapshotId { get; set; }      // GUID
    public string ProjectName { get; set; }
    public string ProjectNumber { get; set; }
    public DateTime Timestamp { get; set; }
    public string RevitVersion { get; set; }
    public string Username { get; set; }
    public List<ElementDTO> Elements { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

### ElementDTO

```csharp
public class ElementDTO
{
    public int ElementId { get; set; }           // Revit ElementId
    public string UniqueId { get; set; }         // Revit UniqueId
    public string Category { get; set; }
    public string FamilyName { get; set; }
    public string TypeName { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
    public GeometryInfo Geometry { get; set; }
}
```

---

## 통신 프로토콜

### RESTful API (DXrevit ↔ DXserver)

**Endpoint**: `POST /api/snapshots`

**Request**:
```json
{
  "snapshotId": "550e8400-e29b-41d4-a716-446655440000",
  "projectName": "Sample Project",
  "timestamp": "2025-10-13T20:00:00Z",
  "elements": [
    {
      "elementId": 123456,
      "uniqueId": "abc-def-ghi",
      "category": "Walls",
      "parameters": {
        "DX_ActivityId": "A001",
        "DX_SyncId": "S001"
      }
    }
  ]
}
```

**Response**:
```json
{
  "success": true,
  "snapshotId": "550e8400-e29b-41d4-a716-446655440000",
  "elementsProcessed": 1523,
  "timestamp": "2025-10-13T20:00:05Z"
}
```

---

## 설정 관리

### 계층 구조

```
1. 하드코딩 기본값 (ConfigurationService.cs)
   ↓ (Override)
2. 설정 파일 (settings.json)
   ↓ (Override)
3. 환경 변수 (DXPLATFORM_*)
   ↓ (Override)
4. 런타임 사용자 설정 (SettingsView)
```

### 설정 파일 위치

```
Windows:
C:\Users\{Username}\AppData\Roaming\DX_Platform\settings.json

macOS (미래):
~/Library/Application Support/DX_Platform/settings.json

Linux (미래):
~/.config/DX_Platform/settings.json
```

### 설정 예시

```json
{
  "apiServerUrl": "https://dx-api.company.com",
  "databaseConnectionString": "",
  "defaultUsername": "user@company.com",
  "timeoutSeconds": 30,
  "batchSize": 100,
  "logFilePath": "C:\\Users\\...\\Logs"
}
```

---

## 로깅 전략

### 로그 레벨

```
ERROR   → 애플리케이션 오류 (예외)
WARNING → 경고 (복구 가능한 문제)
INFO    → 주요 작업 (스냅샷 저장 시작/완료)
DEBUG   → 상세 정보 (개발 전용)
```

### 로그 파일 구조

```
C:\Users\{Username}\AppData\Roaming\DX_Platform\Logs\
├── DX_20251013.log
├── DX_20251012.log
└── DX_20251011.log
```

### 로그 포맷

```
[2025-10-13 20:00:00] [INFO] [DXrevit] 스냅샷 저장 시작
[2025-10-13 20:00:05] [INFO] [DXrevit] 1523개 요소 처리 완료
[2025-10-13 20:00:06] [ERROR] [DXrevit] API 서버 연결 실패
Exception: HttpRequestException
Message: Connection refused
StackTrace: ...
```

### 로그 회전

- **일별 파일**: 매일 자정 새 파일 생성
- **보관 기간**: 30일 (자동 삭제)
- **최대 크기**: 10MB (초과 시 새 파일)

---

## 보안

### 인증

**Phase 3 구현 예정**:
- OAuth 2.0 / OpenID Connect
- JWT 토큰 기반 인증
- API Key (개발/테스트용)

### 데이터 암호화

- **전송 중**: HTTPS (TLS 1.2+)
- **저장 시**: Database-level encryption

### 민감 정보 관리

```csharp
// ❌ 나쁜 예
var connectionString = "Server=...;Password=1234";

// ✅ 좋은 예
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
```

---

## 성능 최적화

### DXrevit

1. **배치 처리**: 100개 요소 단위로 그룹화
2. **비동기 I/O**: `async/await` 패턴
3. **진행률 UI**: `IProgress<T>` 사용

```csharp
for (int i = 0; i < elements.Count; i += batchSize)
{
    var batch = elements.Skip(i).Take(batchSize);
    await SendBatchAsync(batch);
    progress.Report((double)i / elements.Count);
}
```

### DXserver (미래)

1. **캐싱**: Redis (자주 조회되는 데이터)
2. **인덱싱**: PostgreSQL 인덱스 (UniqueId, ElementId)
3. **압축**: gzip (HTTP 응답)

---

## 테스트 전략

### 단위 테스트

```csharp
[Fact]
public void ConfigurationService_LoadSettings_ReturnsDefaultValues()
{
    // Arrange & Act
    var settings = ConfigurationService.LoadSettings();

    // Assert
    Assert.NotNull(settings);
    Assert.Equal("https://localhost:5000", settings.ApiServerUrl);
}
```

### 통합 테스트

```csharp
[Fact]
public async Task ApiClient_SendSnapshot_Success()
{
    // Arrange
    var client = new ApiClient("https://test-server");
    var snapshot = new SnapshotDTO { /* ... */ };

    // Act
    var result = await client.SendSnapshotAsync(snapshot);

    // Assert
    Assert.True(result);
}
```

### E2E 테스트 (수동)

1. Revit 2025 실행
2. 샘플 모델 열기
3. "스냅샷 저장" 버튼 클릭
4. 진행률 UI 확인
5. 로그 파일 확인

---

## 배포 아키텍처

### 개발 환경

```
로컬 PC
├── Revit 2025 (DXrevit 로드)
├── Visual Studio 2022 (개발)
└── Git (버전 관리)
```

### 프로덕션 환경 (미래)

```
클라우드 (Azure/AWS)
├── DXserver (Container)
│   └── PostgreSQL (RDS)
├── Redis (Cache)
└── Blob Storage (파일)

클라이언트 PC
├── Revit 2025 (DXrevit)
└── Navisworks 2025 (DXnavis)
```

---

## 향후 확장 계획

### Phase 3: API 서버
- RESTful API 구현
- PostgreSQL 데이터베이스
- 인증/권한 관리

### Phase 4: Navisworks 통합
- CSV 파서
- Timeliner 자동 연결
- 4D 시뮬레이션

### Phase 5: 고급 기능
- 실시간 동기화 (SignalR)
- 변경 이력 추적 (Diff/Patch)
- 웹 대시보드 (Blazor)

### Phase 6: 확장성
- Civil 3D 지원
- Tekla Structures 지원
- IFC 포맷 지원
