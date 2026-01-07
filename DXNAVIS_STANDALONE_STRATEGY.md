# DXnavis 단독 애드인 개발 전략

> **목표**: DXnavis를 DXBase 의존성 없이 독립적인 애드인으로 개발
> **핵심 기능**: Hierarchy 및 All Properties CSV 출력
> **작성일**: 2025-12-22

---

## 📊 현재 상태 분석

### 1. 프로젝트 의존성 관계

```
┌─────────────┐
│   DXBase    │  (공유 라이브러리)
│ net8.0 +    │  - HttpClientService
│ netstand2.0 │  - ConfigurationService
└──────┬──────┘  - ProjectCodeUtil
       │         - LoggingService
       │
       ├─────────────────┐
       │                 │
       ▼                 ▼
┌─────────────┐   ┌─────────────┐
│  DXrevit    │   │  DXnavis    │
│  net8.0-win │   │  net48      │
│  (Revit API)│   │ (Navis API) │
└─────────────┘   └─────────────┘

직접 의존성 없음 ◄────────►
```

### 2. DXnavis의 DXBase 사용 현황

**사용 파일**: `DXnavis/Services/HierarchyUploader.cs` (1개 파일만)

**사용 기능**:
```csharp
using DXBase.Services;

// 1. HttpClientService - API 통신
var settings = ConfigurationService.LoadSettings();
_httpClient = new HttpClientService(settings.ApiServerUrl, settings.TimeoutSeconds);

// 2. ConfigurationService - 설정 로드
```

**사용 목적**: **API 서버로 계층 데이터 업로드** (v2.0 기능)

---

## ✅ 핵심 발견사항

### DXnavis의 핵심 출력 기능은 DXBase 의존성이 없음!

| 기능 | 파일 | DXBase 의존성 | 상태 |
|------|------|--------------|------|
| **All Properties CSV 출력** | `FullModelExporterService.cs` | ❌ 없음 | ✅ 독립적 |
| **Hierarchy CSV 출력** | `HierarchyFileWriter.cs` | ❌ 없음 | ✅ 독립적 |
| **데이터 추출** | `NavisworksDataExtractor.cs` | ❌ 없음 | ✅ 독립적 |
| **속성 파일 작성** | `PropertyFileWriter.cs` | ❌ 없음 | ✅ 독립적 |
| **API 업로드** | `HierarchyUploader.cs` | ⚠️ 있음 | 선택적 |

**결론**: **목표 기능(Hierarchy + All Properties 출력)은 이미 DXBase 없이 독립적으로 동작 가능**

---

## 🎯 단독 애드인 개발 전략

### Option A: DXBase 의존성 완전 제거 (권장) ⭐

**장점**:
- ✅ 완전히 독립적인 애드인
- ✅ 배포 파일 최소화 (DXBase.dll 불필요)
- ✅ 유지보수 간소화
- ✅ DXrevit와 완전 분리

**단점**:
- ⚠️ API 업로드 기능 제거 필요
- ⚠️ 향후 공유 기능 재구현 필요 (발생 시)

**작업량**: **최소** (HierarchyUploader.cs 제거 또는 간소화)

---

### Option B: DXBase 최소 복사 (부분 의존)

**장점**:
- ✅ API 업로드 기능 유지
- ✅ 향후 확장 가능

**단점**:
- ❌ DXBase.dll 배포 필요
- ❌ DXrevit과 간접 연결 유지

**작업량**: **중간** (현재 상태 유지)

---

### Option C: API 업로드 기능 내재화

**장점**:
- ✅ 모든 기능 유지
- ✅ 완전 독립

**단점**:
- ❌ HttpClient 코드 복사 필요
- ❌ 설정 관리 재구현

**작업량**: **많음** (300-500줄 코드 복사/수정)

---

## 📋 권장 전략: Option A (완전 분리)

### 이유
1. **목표와 완벽히 일치**: Hierarchy + All Properties **출력**만 필요
2. **이미 독립적**: 핵심 기능이 DXBase 없이 동작
3. **배포 간소화**: 단일 DLL만 배포
4. **유지보수 용이**: DXrevit/DXBase 변경에 영향 없음

### 실행 계획

#### Phase 1: DXBase 의존성 제거 (1-2시간)

**Step 1.1**: `HierarchyUploader.cs` 처리
```
Option A-1: 파일 삭제
- API 업로드 기능 완전 제거
- ViewModel에서 UploadToApiCommand 제거

Option A-2: API 기능만 주석 처리
- 파일 유지, API 호출 부분만 비활성화
- 향후 필요 시 재활성화 가능
```

**Step 1.2**: `.csproj` 수정
```xml
<!-- 삭제 -->
<Reference Include="DXBase">
  <HintPath>..\DXBase\bin\Debug\netstandard2.0\DXBase.dll</HintPath>
</Reference>
```

**Step 1.3**: PostBuild 이벤트 수정
```xml
<!-- DXBase.dll 배포 제거 -->
<PostBuildEvent>
echo Deploying DXnavis to Navisworks 2025...
xcopy "$(TargetPath)" "C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\" /Y /I
xcopy "$(TargetDir)System.Text.Json.dll" "C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\" /Y /I
xcopy "$(TargetDir)Newtonsoft.Json.dll" "C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\" /Y /I
echo DXnavis deployment completed!
</PostBuildEvent>
```

**Step 1.4**: ViewModel 수정
```csharp
// DXwindowViewModel.cs

// 삭제 또는 주석 처리
// public ICommand DetectProjectCommand { get; }
// public ICommand UploadToApiCommand { get; }
// private async Task DetectProjectFromCsvAsync() { ... }
// private async Task UploadHierarchyToApiAsync() { ... }
```

**Step 1.5**: XAML UI 수정
```xml
<!-- DXwindow.xaml -->

<!-- API 업로드 섹션 제거 (Grid.Row="3") -->
<!-- 또는 Visibility="Collapsed"로 숨김 -->
```

---

#### Phase 2: 독립 기능 강화 (선택적, 2-4시간)

**Step 2.1**: 간단한 설정 시스템 추가
```csharp
// Services/SimpleSettings.cs
public class SimpleSettings
{
    public string DefaultExportPath { get; set; }
    public string FileNamePattern { get; set; }

    public static SimpleSettings Load()
    {
        // JSON 파일에서 로드
    }

    public void Save()
    {
        // JSON 파일에 저장
    }
}
```

**Step 2.2**: 로깅 추가 (선택적)
```csharp
// Helpers/SimpleLogger.cs
public static class SimpleLogger
{
    public static void Info(string message) { ... }
    public static void Error(string message, Exception ex) { ... }
}
```

---

#### Phase 3: 테스트 및 검증 (1-2시간)

**Step 3.1**: 빌드 검증
```bash
dotnet build DXnavis/DXnavis.csproj
# 오류 없음 확인
```

**Step 3.2**: 기능 테스트
- [ ] "전체 속성 CSV 저장" 버튼 동작 확인
- [ ] "계층 구조 내보내기" 버튼 동작 확인
- [ ] CSV 파일 생성 확인
- [ ] 진행률 UI 업데이트 확인

**Step 3.3**: 배포 테스트
```bash
# PostBuild 이벤트 실행
# C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\ 확인
# DXnavis.dll만 있고 DXBase.dll 없음 확인
```

---

## 📁 수정 대상 파일 목록

### 필수 수정 (Phase 1)

| 파일 | 작업 | 예상 시간 |
|------|------|-----------|
| `DXnavis.csproj` | DXBase 참조 제거 | 5분 |
| `HierarchyUploader.cs` | 삭제 또는 주석 처리 | 10분 |
| `DXwindowViewModel.cs` | API 명령 제거 | 15분 |
| `DXwindow.xaml` | API UI 섹션 제거 | 10분 |

**총 예상 시간**: 40분

### 선택 추가 (Phase 2)

| 파일 | 작업 | 예상 시간 |
|------|------|-----------|
| `Services/SimpleSettings.cs` | 신규 생성 | 30분 |
| `Helpers/SimpleLogger.cs` | 신규 생성 | 20분 |
| ViewModel 설정 통합 | 기존 코드 수정 | 30분 |

**총 예상 시간**: 1시간 20분

---

## 🔄 롤백 계획

문제 발생 시 롤백:

```bash
# 1. Git으로 원복
git checkout HEAD -- DXnavis/

# 2. 또는 백업에서 복원
# (작업 전 브랜치 생성 권장)
git checkout -b dxnavis-standalone
```

---

## ✅ 완료 조건

- [ ] DXnavis.csproj에 DXBase 참조 없음
- [ ] 빌드 오류 없음
- [ ] "전체 속성 CSV 저장" 기능 정상 동작
- [ ] "계층 구조 내보내기" 기능 정상 동작
- [ ] 배포 폴더에 DXBase.dll 없음
- [ ] Navisworks에서 애드인 로드 성공

---

## 🎯 최종 구조 (완료 후)

```
DXnavis/ (독립 애드인)
├─ Services/
│  ├─ FullModelExporterService.cs     ✅ All Properties CSV
│  ├─ HierarchyFileWriter.cs          ✅ Hierarchy CSV
│  ├─ NavisworksDataExtractor.cs      ✅ 데이터 추출
│  ├─ PropertyFileWriter.cs           ✅ 속성 파일 작성
│  └─ [HierarchyUploader.cs]          ❌ 삭제됨 (또는 주석)
├─ ViewModels/
│  └─ DXwindowViewModel.cs            ✅ API 명령 제거
├─ Views/
│  └─ DXwindow.xaml                   ✅ 출력 기능만 표시
└─ DXnavis.csproj                     ✅ DXBase 참조 없음

배포:
C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\
├─ DXnavis.dll                        (단일 DLL)
├─ System.Text.Json.dll
└─ Newtonsoft.Json.dll
```

---

## 💡 향후 확장 고려사항

### API 업로드 기능이 다시 필요해질 경우

**Option 1**: DXBase 재참조
- 가장 간단
- DXBase.dll 배포 필요

**Option 2**: 간단한 HTTP 클라이언트 내재화
```csharp
// Services/SimpleHttpClient.cs
public class SimpleHttpClient
{
    private readonly HttpClient _client;

    public async Task<string> PostJsonAsync(string url, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(url, content);
        return await response.Content.ReadAsStringAsync();
    }
}
```
**코드량**: ~100줄

---

## 📝 Codex 협업 가이드

### Codex에게 요청할 작업

1. **Phase 1 자동화**
```
Task: DXnavis에서 DXBase 의존성 완전 제거

Files to modify:
- DXnavis/DXnavis.csproj (DXBase 참조 제거)
- DXnavis/Services/HierarchyUploader.cs (삭제)
- DXnavis/ViewModels/DXwindowViewModel.cs (API 명령 제거)
- DXnavis/Views/DXwindow.xaml (API UI 제거)

Keep intact:
- FullModelExporterService.cs
- HierarchyFileWriter.cs
- NavisworksDataExtractor.cs
- PropertyFileWriter.cs
```

2. **빌드 검증**
```
Task: DXnavis 프로젝트 빌드 및 오류 수정

Commands:
1. dotnet build DXnavis/DXnavis.csproj
2. Fix any compilation errors
3. Verify no warnings related to missing references
```

3. **기능 테스트 가이드 생성**
```
Task: DXnavis 독립 애드인 테스트 계획서 작성

Include:
- Manual test steps
- Expected results
- CSV output validation
- Performance benchmarks
```

---

## 🚀 즉시 실행 가능한 명령

### Step 1: 백업 및 브랜치 생성
```bash
git checkout -b dxnavis-standalone
git add .
git commit -m "backup: Before DXnavis standalone refactoring"
```

### Step 2: DXBase 참조 제거
```bash
# .csproj 파일 수동 편집
# 118-120번 줄 삭제
```

### Step 3: 불필요한 파일 제거
```bash
# Option A-1: 완전 삭제
rm DXnavis/Services/HierarchyUploader.cs

# Option A-2: 백업 후 삭제
mv DXnavis/Services/HierarchyUploader.cs DXnavis/Services/HierarchyUploader.cs.bak
```

### Step 4: 빌드 테스트
```bash
dotnet build DXnavis/DXnavis.csproj
```

---

## 📊 예상 결과

### Before (현재)
- **DLL 개수**: 2개 (DXnavis.dll + DXBase.dll)
- **기능**: Hierarchy/All Properties 출력 + API 업로드
- **의존성**: DXBase, DXrevit 간접 연결

### After (완료 후)
- **DLL 개수**: 1개 (DXnavis.dll)
- **기능**: Hierarchy/All Properties 출력 (목표 달성)
- **의존성**: 완전 독립

**개선율**:
- 배포 파일 크기: **-30%** (DXBase.dll 제거)
- 유지보수 복잡도: **-50%** (의존성 제거)
- 빌드 시간: **-20%** (참조 감소)

---

**다음 단계**: Codex와 함께 Phase 1 실행 시작

1. 브랜치 생성
2. DXBase 참조 제거
3. 빌드 검증
4. 기능 테스트
5. 커밋 및 문서화
