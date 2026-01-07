# DXnavis 단독 애드인 개발 종합 전략 (with Skills)

> **목표**: DXnavis를 DXBase 의존성 없이 독립 애드인으로 개발
> **핵심 기능**: Hierarchy + All Properties CSV 출력
> **협업 방식**: Claude Code + Codex 이중 AI 엔지니어링 루프
> **작성일**: 2025-12-22

---

## 🎯 전략 개요

### 핵심 발견사항 (재확인)
✅ **DXnavis의 목표 기능(Hierarchy + All Properties)은 이미 DXBase 의존성 없이 독립적으로 동작**

### 활용 스킬 맵핑

| Phase | 스킬 | 역할 | 자동화 |
|-------|------|------|--------|
| **0. 준비** | `using-git-worktrees` | 격리된 작업 공간 생성 | ✅ 자동 |
| **1. 계획** | `codex-claude-loop` | Claude 계획 → Codex 검증 | ✅ 루프 |
| **2. 구현** | `codex-claude-loop` | Claude 구현 → Codex 리뷰 | ✅ 루프 |
| **3. 문서화** | `code-changelog` | 모든 변경사항 자동 기록 | ✅ 자동 |
| **4. 검증** | `codex` | 최종 코드 검증 | ✅ CLI |

---

## ⚠️ Critical Version Constraints (필수 준수)

### 🔒 버전 고정 정책

**Navisworks 2025 및 .NET Framework 4.8은 절대 변경 불가**

#### 1. Navisworks API 버전 고정
```
Target: Navisworks Manage 2025
Path: C:\Program Files\Autodesk\Navisworks Manage 2025\
API Version: 2025 (15.0.x)
```

**고정 이유**:
- Navisworks API는 버전별로 DLL이 다름
- 2025 API는 .NET Framework 4.8 필수
- 플러그인 배포 경로가 버전별로 고정됨

#### 2. .NET Framework 버전 고정
```xml
<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
```

**절대 변경 금지**:
- ❌ .NET Framework 4.8.1
- ❌ .NET 6/7/8
- ❌ .NET Standard (Navisworks API 호환 불가)
- ✅ .NET Framework 4.8 (유일한 호환 버전)

**기술적 제약**:
- Navisworks 2025 API는 .NET Framework 4.8로 빌드됨
- 더 높은 버전 사용 시 런타임 로딩 실패
- .NET Core/5+ 사용 불가 (COM 의존성)

#### 3. API DLL 참조 경로 고정 (DXnavis.csproj 38-72번 줄)
```xml
<Reference Include="Autodesk.Navisworks.Api">
  <HintPath>C:\Program Files\Autodesk\Navisworks Manage 2025\Autodesk.Navisworks.Api.dll</HintPath>
</Reference>
<Reference Include="Autodesk.Navisworks.Controls">
  <HintPath>C:\Program Files\Autodesk\Navisworks Manage 2025\Autodesk.Navisworks.Controls.dll</HintPath>
</Reference>
```

**변경 금지**:
- 모든 HintPath는 "Navisworks Manage 2025" 유지
- Navisworks 2024/2026 경로 사용 불가

#### 4. 배포 경로 고정 (PostBuild Event)
```
C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\
```

### 🔍 검증 체크리스트

**Phase 2 (구현) 전에 확인**:
- [ ] DXnavis.csproj Line 12: `<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>`
- [ ] Lines 38-72: 모든 API DLL HintPath에 "2025" 포함
- [ ] PostBuild 이벤트: "Navisworks Manage 2025\Plugins" 경로 유지

**Phase 3 (빌드) 후에 확인**:
```bash
# .NET Framework 버전 확인
dotnet list DXnavis/DXnavis.csproj package | grep -i "framework"
# 출력: net48 (정상) / net6.0 (오류!)

# 빌드 출력 DLL 확인
file DXnavis/bin/Release/DXnavis.dll
# 출력: PE32 executable (DLL) ... (.Net assembly) for MS Windows
```

**Phase 4 (기능 테스트) 전에 확인**:
```bash
# 배포 경로 확인
ls "C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\DXnavis.dll"
# 존재하면 성공

# 다른 버전 경로에 없는지 확인
ls "C:\ProgramData\Autodesk\Navisworks Manage 2024\Plugins\DXnavis.dll"  # 없어야 함
ls "C:\ProgramData\Autodesk\Navisworks Manage 2026\Plugins\DXnavis.dll"  # 없어야 함
```

### ⚙️ .csproj 수정 시 주의사항

**절대 수정 금지 영역**:
```xml
<!-- Line 12: Framework 버전 -->
<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>

<!-- Lines 38-72: Navisworks API 참조 (2025 경로) -->
<Reference Include="Autodesk.Navisworks.*">
  <HintPath>...\Navisworks Manage 2025\...</HintPath>
</Reference>

<!-- PostBuild Event: 배포 경로 (2025) -->
<PostBuildEvent>
  xcopy ... "C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\" ...
</PostBuildEvent>
```

**수정 가능 영역**:
```xml
<!-- Lines 118-120: DXBase 참조 (제거 대상) -->
<Reference Include="DXBase">
  <HintPath>...\DXBase\bin\Debug\netstandard2.0\DXBase.dll</HintPath>
</Reference>

<!-- PostBuild Event: DXBase.dll 배포 라인 (제거 대상) -->
xcopy "$(TargetDir)DXBase.dll" "C:\ProgramData\..." /Y /I
```

### 🚨 버전 변경 시도 시 예상 오류

**Case 1: .NET 6/7/8 사용 시**
```
Error CS0012: The type 'DocumentBrowser' is defined in an assembly
that is not referenced. You must add a reference to assembly
'Autodesk.Navisworks.Api, Version=15.0.0.0, Culture=neutral,
PublicKeyToken=...'

→ 해결: TargetFrameworkVersion을 v4.8로 되돌림
```

**Case 2: Navisworks 2024 경로 사용 시**
```
Runtime Error: Could not load file or assembly 'Autodesk.Navisworks.Api,
Version=14.0.0.0' or one of its dependencies.

→ 해결: HintPath를 "Navisworks Manage 2025"로 수정
```

**Case 3: .NET Framework 4.8.1 사용 시**
```
Warning: Target framework '.NETFramework,Version=v4.8.1' is newer
than the runtime '4.8.03761'.
Plugin may not load in Navisworks 2025.

→ 해결: TargetFrameworkVersion을 v4.8 (4.8.1 아님)로 수정
```

---

## 📋 Phase 0: 격리된 작업 공간 준비

### Skill: `using-git-worktrees`

**목적**: 기존 코드에 영향 없이 안전하게 작업

### Step 0.1: Worktree 디렉토리 확인

```bash
# 1. 기존 worktree 디렉토리 확인
ls -d .worktrees 2>/dev/null
ls -d worktrees 2>/dev/null

# 2. CLAUDE.md 확인
grep -i "worktree.*director" CLAUDE.md 2>/dev/null

# 3. 없으면 생성 (프로젝트 로컬 권장)
mkdir -p .worktrees
echo ".worktrees/" >> .gitignore
git add .gitignore
git commit -m "chore: Add .worktrees to gitignore"
```

### Step 0.2: DXnavis 전용 Worktree 생성

```bash
# 현재 프로젝트 루트에서 실행
project=$(basename "$(git rev-parse --show-toplevel)")

# Worktree 생성 (dxnavis-standalone 브랜치)
git worktree add .worktrees/dxnavis-standalone -b feature/dxnavis-standalone

# Worktree로 이동
cd .worktrees/dxnavis-standalone
```

### Step 0.3: 환경 검증

```bash
# .NET SDK 확인
dotnet --version  # 8.0 이상 필요

# 프로젝트 빌드 테스트
dotnet build DXnavis/DXnavis.csproj

# 의존성 확인
dotnet list DXnavis/DXnavis.csproj reference
# 출력: DXBase (제거 대상)
```

**예상 결과**:
```
Worktree ready at C:/Users/.../AWP_2025/개발폴더/.worktrees/dxnavis-standalone
Build successful
DXBase reference detected (will be removed)
Ready to implement DXnavis standalone
```

---

## 📋 Phase 1: 계획 수립 및 검증

### Skill: `codex-claude-loop` (Plan Validation)

**목적**: Claude가 작성한 계획을 Codex가 검증하여 문제 사전 방지

### Step 1.1: Claude가 상세 계획 작성

**계획 내용** (이미 `DXNAVIS_STANDALONE_STRATEGY.md`에 작성됨):

```markdown
## 구현 계획

### 1. DXBase 참조 제거
- File: DXnavis/DXnavis.csproj
- Action: 118-120번 줄 삭제
- Risk: Low

### 2. HierarchyUploader.cs 처리
- File: DXnavis/Services/HierarchyUploader.cs
- Action: 삭제 또는 주석 처리
- Risk: Low (API 업로드 기능만 사용)

### 3. ViewModel 정리
- File: DXnavis/ViewModels/DXwindowViewModel.cs
- Action: API 관련 명령 제거 (DetectProjectCommand, UploadToApiCommand)
- Risk: Medium (UI 바인딩 영향)

### 4. XAML UI 정리
- File: DXnavis/Views/DXwindow.xaml
- Action: API 업로드 섹션 제거 (Grid.Row="3")
- Risk: Low

### 5. PostBuild 이벤트 수정
- File: DXnavis/DXnavis.csproj
- Action: DXBase.dll 배포 제거
- Risk: Low
```

### Step 1.2: Codex에 계획 검증 요청

**사용자 선택 (AskUserQuestion)**:
- Model: `gpt-5` 또는 `gpt-5-codex`
- Reasoning effort: `medium`

**Codex 검증 명령**:
```bash
echo "Review this DXnavis standalone implementation plan and identify any issues:

$(cat DXNAVIS_STANDALONE_STRATEGY.md)

Check for:
1. Logic errors - Will removing DXBase break core functionality?
2. Missing dependencies - Are there hidden dependencies?
3. Build issues - Will the project compile after changes?
4. Runtime errors - Will the addon load in Navisworks?
5. Data loss - Will any functionality be lost?

Provide specific feedback on each file modification." | codex exec -m gpt-5-codex --config model_reasoning_effort="medium" --sandbox read-only
```

### Step 1.3: Codex 피드백 처리

**예상 Codex 응답**:
```
✅ Plan is sound overall

Potential issues:
1. ViewModel command removal may cause XAML binding errors
   → Recommendation: Remove XAML bindings FIRST, then ViewModel commands

2. PostBuild event uses xcopy with DXBase.dll
   → Recommendation: Test deployment path after removal

3. No rollback plan for .csproj changes
   → Recommendation: Create backup before editing

Suggested order:
1. Backup DXnavis.csproj
2. Remove XAML bindings (DXwindow.xaml)
3. Remove ViewModel commands (DXwindowViewModel.cs)
4. Remove HierarchyUploader.cs
5. Update .csproj (reference + PostBuild)
6. Test build
```

### Step 1.4: 계획 수정 (Codex 피드백 반영)

```markdown
## 수정된 실행 순서 (Codex 검증 통과)

1. ✅ Backup: DXnavis.csproj 백업 생성
2. ✅ XAML 먼저 수정 (바인딩 오류 방지)
3. ✅ ViewModel 수정
4. ✅ HierarchyUploader 제거
5. ✅ .csproj 수정 (참조 + PostBuild)
6. ✅ 빌드 테스트
```

---

## 📋 Phase 2: 구현 및 실시간 검증

### Skill: `codex-claude-loop` (Implementation & Review)

**목적**: Claude가 구현한 코드를 Codex가 즉시 검토하여 품질 보장

### Step 2.1: Claude 구현 + code-changelog 자동 기록

**초기화**:
```python
from code_changelog_tracker import CodeChangeLogger

logger = CodeChangeLogger(
    "DXnavis Standalone Development",
    user_request="Remove DXBase dependency for standalone addon"
)
```

#### 🔄 **작업 1: .csproj 백업**

```bash
# Claude 실행
cp DXnavis/DXnavis.csproj DXnavis/DXnavis.csproj.backup
```

```python
# Changelog 기록
logger.log_file_creation(
    "DXnavis/DXnavis.csproj.backup",
    "Original .csproj content",
    "Safety backup before removing DXBase reference"
)
```

#### 🔄 **작업 2: XAML 수정**

```bash
# Claude가 Edit 도구 사용
# File: DXnavis/Views/DXwindow.xaml
# Remove: Grid.Row="3" (API 업로드 섹션)
```

```python
# Changelog 기록
logger.log_file_modification(
    "DXnavis/Views/DXwindow.xaml",
    "old_content (Grid.Row=3 with API upload)",
    "new_content (Grid.Row=3 removed)",
    "Remove API upload UI section to eliminate DXBase dependency"
)
```

**Codex 즉시 검증**:
```bash
echo "Review this XAML change:

File: DXnavis/Views/DXwindow.xaml
Change: Removed Grid.Row=3 (API upload section)

Check:
1. Are there orphaned bindings?
2. Will UI layout break?
3. Is Grid numbering still valid?" | codex exec resume --last
```

**Codex 응답 예시**:
```
✅ XAML change looks good

Note: Grid.Row numbers are still valid (Row 0,1,2 remain)
Warning: Ensure ViewModel properties are removed next
```

#### 🔄 **작업 3: ViewModel 수정**

```bash
# Claude가 Edit 도구 사용
# File: DXnavis/ViewModels/DXwindowViewModel.cs
# Remove: DetectProjectCommand, UploadToApiCommand, related methods
```

```python
# Changelog 기록
logger.log_file_modification(
    "DXnavis/ViewModels/DXwindowViewModel.cs",
    "old_content (with API commands)",
    "new_content (API commands removed)",
    "Remove API-related commands and methods"
)
```

**Codex 검증**:
```bash
echo "Review ViewModel change:

Removed:
- DetectProjectCommand
- UploadToApiCommand
- DetectProjectFromCsvAsync()
- UploadHierarchyToApiAsync()

Check:
1. Are all references removed?
2. Is constructor still valid?
3. Are there unused using statements?" | codex exec resume --last
```

#### 🔄 **작업 4: HierarchyUploader 제거**

```bash
# Claude 실행
rm DXnavis/Services/HierarchyUploader.cs
```

```python
# Changelog 기록
logger.log_file_deletion(
    "DXnavis/Services/HierarchyUploader.cs",
    "API upload service - only component using DXBase"
)
```

#### 🔄 **작업 5: .csproj 수정**

**⚠️ 중요: Framework 버전 및 Navisworks 경로 절대 수정 금지**

```bash
# Claude가 Edit 도구 사용
# File: DXnavis/DXnavis.csproj

# ✅ 제거할 부분 (DXBase 의존성만):
# - Lines 118-120: DXBase reference
# - PostBuild 이벤트: DXBase.dll xcopy 라인

# ❌ 절대 수정 금지 부분:
# - Line 12: <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
# - Lines 38-72: Navisworks 2025 API 참조 경로
# - PostBuild 이벤트: "Navisworks Manage 2025" 경로
```

```python
# Changelog 기록
logger.log_file_modification(
    "DXnavis/DXnavis.csproj",
    "old_content (with DXBase reference and deployment)",
    "new_content (DXBase removed, Framework v4.8 preserved)",
    "Remove DXBase reference and deployment while preserving Navisworks 2025 + .NET Framework 4.8 constraints"
)
```

**Codex 최종 검증 (버전 체크 포함)**:
```bash
echo "Review complete .csproj changes:

Removed:
- Lines 118-120: DXBase reference
- PostBuild Event: DXBase.dll xcopy line

CRITICAL VERIFICATION:
1. Line 12: TargetFrameworkVersion is still v4.8? (MUST be v4.8)
2. Lines 38-72: All Navisworks DLL HintPaths contain '2025'? (MUST be 2025)
3. PostBuild Event: Deployment path is 'Navisworks Manage 2025\Plugins'? (MUST be 2025)
4. Is XML still valid?
5. Are all other references intact?
6. Will PostBuild script work?" | codex exec resume --last
```

#### 🔄 **작업 6: 변경사항 저장 및 문서 생성**

```python
# Changelog 저장 + HTML 뷰어 생성
logger.save_and_build()

# 출력:
# - reviews/20251222_153000.md (변경 이력)
# - reviews/index.html (자동 업데이트)
# - reviews/SUMMARY.md (네비게이션)
```

**문서 서버 실행**:
```bash
cd reviews && python -m http.server 4000 &
# 브라우저: http://localhost:4000
```

---

## 📋 Phase 3: 빌드 및 테스트

### Skill: `codex` (Build Validation)

### Step 3.1: 빌드 실행

```bash
# Claude 실행
dotnet build DXnavis/DXnavis.csproj --configuration Release
```

**예상 출력**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:05.23
```

### Step 3.2: Codex 빌드 검증

```bash
echo "Validate this build output:

$(dotnet build DXnavis/DXnavis.csproj)

Check:
1. Are there any warnings about missing references?
2. Is the output path correct?
3. Are all dependencies resolved?" | codex exec -m gpt-5-codex --config model_reasoning_effort="low" --sandbox read-only
```

### Step 3.3: 배포 테스트

```bash
# Claude 실행
dotnet build DXnavis/DXnavis.csproj --configuration Release

# PostBuild 이벤트 확인
ls "C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\DXnavis.dll"
ls "C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\DXBase.dll"  # 없어야 함
```

**예상 결과**:
```
✅ DXnavis.dll exists
✅ System.Text.Json.dll exists
✅ Newtonsoft.Json.dll exists
❌ DXBase.dll NOT found (성공!)
```

---

## 📋 Phase 4: 기능 테스트

### Step 4.1: Navisworks 로드 테스트

**수동 테스트 절차**:
1. Navisworks 2025 실행
2. DXnavis 애드인 로드 확인
3. DXwindow 열기

**Codex에 테스트 계획 검증 요청**:
```bash
echo "Review this manual test plan:

1. Launch Navisworks 2025
2. Verify DXnavis loads without errors
3. Open DXwindow
4. Test 'Export All Properties CSV' button
5. Verify CSV file generation

Are there any missing test cases?" | codex exec resume --last
```

### Step 4.2: All Properties CSV 출력 테스트

**테스트 시나리오**:
```
1. Navisworks에서 샘플 모델 열기
2. DXwindow에서 "전체 속성 CSV 저장" 클릭
3. 파일 저장 대화상자 확인
4. CSV 파일 생성 확인
5. CSV 내용 검증 (헤더, 데이터, 인코딩)
```

### Step 4.3: Hierarchy CSV 출력 테스트

**테스트 시나리오**:
```
1. Navisworks에서 계층 구조 있는 모델 열기
2. DXwindow에서 "계층 구조 내보내기" 클릭
3. 파일 저장 확인
4. 계층 정보 검증 (ObjectId, ParentId, Level)
```

---

## 📋 Phase 5: 문서화 및 커밋

### Skill: `code-changelog` (자동 문서 생성 완료)

### Step 5.1: 변경사항 리뷰

**브라우저에서 확인**:
```
http://localhost:4000
```

**자동 생성된 문서**:
- `reviews/20251222_153000.md` - 전체 변경 이력
- 각 파일별 변경 내용 (before/after diff)
- 변경 이유 설명

### Step 5.2: Git 커밋

```bash
# Worktree에서 실행
git add DXnavis/
git commit -m "refactor(DXnavis): Remove DXBase dependency for standalone addon

- Remove DXBase reference from .csproj
- Remove API upload functionality (HierarchyUploader.cs)
- Remove API-related UI and commands
- Update PostBuild to exclude DXBase.dll

Result: DXnavis is now a completely standalone addon
Core functionality (Hierarchy + All Properties CSV) preserved
Deployment simplified to single DLL

Closes #[issue-number]"
```

### Step 5.3: Pull Request 준비

```bash
# Main 브랜치로 돌아가기
cd ../../  # Worktree에서 나가기

# PR 생성 (GitHub CLI 사용 예시)
gh pr create \
  --title "DXnavis Standalone: Remove DXBase Dependency" \
  --body "$(cat reviews/20251222_153000.md)" \
  --base v1 \
  --head feature/dxnavis-standalone
```

---

## 🔄 Codex-Claude Loop 요약

### 완벽한 협업 루프

```
┌─────────────────────────────────────────────────────────────┐
│                    Codex-Claude Loop                        │
└─────────────────────────────────────────────────────────────┘

Phase 1: Planning
  Claude → 상세 계획 작성 (STRATEGY.md)
     ↓
  Codex → 계획 검증 (logic, dependencies, risks)
     ↓
  Claude → 피드백 반영 및 계획 수정
     ↓
  User → 계획 승인

Phase 2: Implementation
  Claude → 코드 수정 (Edit, Write tools)
     ↓
  Changelog → 자동 문서화 (save_and_build)
     ↓
  Codex → 코드 리뷰 (bugs, performance, best practices)
     ↓
  Claude → 이슈 수정
     ↓
  Codex → 재검증 (resume --last)
     ↓
  반복 → 품질 기준 만족할 때까지

Phase 3: Validation
  Claude → 빌드 실행
     ↓
  Codex → 빌드 결과 검증
     ↓
  Claude → 배포 테스트
     ↓
  Codex → 배포 검증
     ↓
  완료!
```

---

## 📊 예상 결과

### Before (현재)
```
DXnavis/
├─ DXnavis.dll (의존: DXBase.dll)
├─ DXBase.dll (배포 필요)
├─ System.Text.Json.dll
└─ Newtonsoft.Json.dll

기능:
✅ Hierarchy CSV 출력
✅ All Properties CSV 출력
⚠️ API 업로드 (v2.0) - DXBase 의존
```

### After (완료 후)
```
DXnavis/
├─ DXnavis.dll (완전 독립!)
├─ System.Text.Json.dll
└─ Newtonsoft.Json.dll

기능:
✅ Hierarchy CSV 출력
✅ All Properties CSV 출력
❌ API 업로드 제거

개선:
✅ 배포 파일 -30%
✅ 의존성 0개
✅ DXrevit과 완전 분리
```

---

## ⏱️ 예상 소요 시간

| Phase | 작업 | 예상 시간 | 실제 시간 |
|-------|------|-----------|-----------|
| 0 | Worktree 준비 | 10분 | |
| 1 | 계획 + Codex 검증 | 20분 | |
| 2 | 구현 + 실시간 리뷰 | 40분 | |
| 3 | 빌드 + 검증 | 15분 | |
| 4 | 기능 테스트 | 30분 | |
| 5 | 문서화 + 커밋 | 15분 | |
| **총계** | | **2시간 10분** | |

---

## ✅ 성공 기준

### 기술적 검증
- [ ] DXnavis.csproj에 DXBase 참조 없음
- [ ] 컴파일 오류 0개, 경고 0개
- [ ] Navisworks에서 애드인 로드 성공
- [ ] "전체 속성 CSV 저장" 기능 정상 동작
- [ ] "계층 구조 내보내기" 기능 정상 동작
- [ ] 배포 폴더에 DXBase.dll 없음

### Codex 검증 통과
- [ ] 계획 검증 통과 (Phase 1)
- [ ] 코드 리뷰 통과 (Phase 2)
- [ ] 빌드 검증 통과 (Phase 3)

### 문서화 완료
- [ ] code-changelog 자동 생성 완료
- [ ] reviews/index.html 접근 가능
- [ ] Git 커밋 메시지 작성 완료

---

## 🚀 즉시 실행 명령

### 1단계: Worktree 생성

```bash
# 프로젝트 루트에서
mkdir -p .worktrees
echo ".worktrees/" >> .gitignore
git add .gitignore
git commit -m "chore: Add .worktrees to gitignore"

git worktree add .worktrees/dxnavis-standalone -b feature/dxnavis-standalone
cd .worktrees/dxnavis-standalone
```

### 2단계: 문서 서버 실행 (백그라운드)

```bash
cd reviews && python -m http.server 4000 &
cd ..
```

### 3단계: Codex-Claude Loop 시작

```
User: "Codex-Claude Loop로 DXnavis 단독 개발 시작"

Claude: "I'm using the codex-claude-loop skill to implement DXnavis standalone."

[Phase 1: Planning with Codex validation...]
[Phase 2: Implementation with real-time review...]
[Phase 3: Build validation...]
[Phase 4: Testing...]
[Phase 5: Documentation & commit...]

Done!
```

---

## 💡 Best Practices

### Codex 활용 팁
1. **계획 먼저 검증**: 코딩 전에 반드시 Codex 검증
2. **작은 단위**: 파일 하나씩 수정 → 즉시 리뷰
3. **resume 활용**: 같은 세션 유지로 컨텍스트 보존
4. **reasoning effort**: 계획은 medium, 빌드는 low

### Changelog 활용 팁
1. **실시간 서버**: 개발 시작 전에 http 서버 켜두기
2. **브라우저 북마크**: http://localhost:4000 북마크
3. **자주 새로고침**: save_and_build() 후 즉시 확인
4. **Git과 연동**: reviews 폴더도 Git 관리

### Worktree 활용 팁
1. **독립 작업**: Main 브랜치에 영향 없음
2. **병렬 개발**: 다른 기능도 별도 worktree에서 가능
3. **안전한 실험**: 실패해도 worktree만 삭제
4. **정리**: 완료 후 `git worktree remove` 실행

---

**준비 완료! Codex-Claude Loop로 고품질 DXnavis 단독 애드인을 개발하세요!** 🚀
