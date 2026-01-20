# Changelog

All notable changes to DXTnavis will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.2.1] - Planned 🚧

### Bug Fix: TextBox IME 입력 오류

#### 증상
- Schedule Builder 탭 TextBox에서 한글/영어/숫자 입력 불가
- IME (Input Method Editor) 조합 문자 입력 차단

#### 원인
- `TextBox_PreviewKeyDown` 핸들러에서 `Key.ImeProcessed` 미처리

#### 수정 내용 (Planned)
- [ ] `Key.ImeProcessed` 처리 추가
- [ ] 모든 TextBox 입력 테스트 검증

#### 영향 파일
- `Views/DXwindow.xaml.cs`

---

## [1.2.0] - 2026-01-21

### Direct TimeLiner Execution (Phase 14)

**🎯 목표: CSV 없이 1클릭으로 TimeLiner 연결**

#### 직접 실행 기능
- **ExecuteDirectToTimeLiner()** - CSV 중간 단계 없이 직접 TimeLiner 연결
- **ConvertPreviewToScheduleData()** - PreviewItems → ScheduleData 변환
- **DirectExecuteCommand** - 새로운 커맨드 바인딩

#### DryRun 모드
- **IsDryRunMode** - 실행 전 미리보기 옵션
- **미리보기 보고서** - ParentSet별 Task 수, 샘플 Task 정보 표시

#### 진행률 표시
- **ExecutionProgress** - 0-100% 진행률 추적
- **IsExecuting** - 실행 중 상태 플래그
- **ProgressBar UI** - 실시간 진행률 표시

#### 워크플로우 개선
- **7단계 → 3단계** - 57% 워크플로우 단축
- **원클릭 연결** - 객체 선택 → 설정 → 직접 실행

#### Modified Files
- `ScheduleBuilderViewModel.cs` - DirectExecuteCommand, ConvertPreviewToScheduleData 추가
- `DXwindow.xaml` - 직접 실행 버튼, DryRun 체크박스, ProgressBar 추가

---

## [1.1.0] - 2026-01-21

### TimeLiner Enhancement (Phase 13)

**🎯 목표: TimeLiner 직접 연동 강화**

#### TaskType 한글화
- **UI 한글 표시** - 구성/철거/임시로 Schedule Builder 표시
- **CSV 영문 변환** - 저장 시 Construct/Demolish/Temporary로 자동 변환
- **양방향 매핑** - `TaskTypeKorToEng`, `TaskTypeEngToKor` 딕셔너리

#### DateMode 옵션
- **PlannedOnly** - 계획일만 설정
- **ActualFromPlanned (권장)** - 계획일을 실제일에도 복사
- **BothSeparate** - 계획/실제 별도 입력
- **CSV 확장** - ActualStart/ActualEnd 컬럼 자동 포함

#### 확장 ParentSet 전략
- **ByLevel** - 트리 레벨 (depth) 기반
- **ByFloorLevel** - 건축 층 (Element.Level 속성)
- **ByCategory** - Element 카테고리
- **ByArea** - 구역 (Element.Area/Zone)
- **Composite** - Level + Category 조합
- **ByProperty** - SysPath 기반
- **Custom** - 사용자 입력

#### New Files
- `Models/DateMode.cs` - DateMode enum 및 확장 메서드

#### Modified Files
- `ScheduleBuilderViewModel.cs` - TaskType 한글화, DateMode, 확장 ParentSet
- `TimeLinerService.cs` - 한글 TaskType 파싱 강화
- `DXwindow.xaml` - DateMode ComboBox 추가

---

## [1.0.0] - 2026-01-20

### Grouped Data Structure Refactoring (Phase 12)

**🎯 핵심 최적화: 445K records → ~5K groups**

- **그룹화 데이터 구조** - 기본 데이터 구조를 그룹 기반으로 변경
- **체크박스 필터 UI** - Level, Category 필터를 체크박스 다중 선택 방식으로 변경
- **그룹 단위 Select All** - 445K 개별 레코드 대신 ~5K 그룹 단위 처리로 성능 대폭 향상
- **TimeLiner 호환성 유지** - `ObjectGroupModel.ToHierarchicalRecords()` 메서드로 기존 기능 호환

### New Models
- `ObjectGroupModel.cs` - 객체 그룹화 모델 (1 object = 1 group with properties)
- `PropertyRecord.cs` - 간소화된 속성 레코드 (객체 정보 제외)
- `FilterOption.cs` - 체크박스 기반 필터 옵션 모델

### Architecture Changes
- **기본 뷰 변경** - ListView + Expander가 기본 뷰 (그룹화 토글 제거)
- **필터 시스템** - ComboBox → 체크박스 다중 선택
- **데이터 로딩** - `ExtractAllAsGroups()` 메서드로 그룹 단위 추출
- **호환성 레이어** - `GetSelectedHierarchicalRecords()`, `GetSelectedObjectIds()` 메서드 추가

### Performance
- **Select All**: 445K iterations → ~5K iterations (약 99% 감소)
- **필터링**: 그룹 단위 필터링으로 UI 응답성 향상
- **메모리**: 중복 객체 정보 제거로 메모리 효율 개선

---

## [0.9.0] - 2026-01-20

### Object Grouping MVP (Phase 11)
- **객체별 그룹화 보기** - 동일 객체의 속성들을 그룹으로 묶어서 표시
- **Flat/Grouped Mode 전환** - 중앙 패널에 토글 체크박스로 전환
- **Expander UI** - 객체별 접기/펼치기 지원
- **그룹 선택 전파** - 그룹 선택 시 하위 속성 모두 선택, 개별 선택도 지원
- **성능 최적화** - 10,000개 미만 필터링 데이터에서만 그룹화 활성화

### New Files
- `ObjectGroupViewModel.cs` - 객체 그룹화 ViewModel

### UI
- **Grouped View 토글** 추가 - Select All 옆에 체크박스 추가
- **ListView + Expander** - 그룹화 모드 시 계층적 표시
- **조건부 활성화** - 필터링 결과가 10K 미만일 때만 그룹화 사용 가능

### Converter Update
- `BoolToVisibilityConverter` - Invert 파라미터 지원 추가

**→ [Phase 11 Document](docs/phases/phase-11-object-grouping.md)**

---

## [0.8.0] - 2026-01-19

### Schedule Builder (Phase 10)
- **Schedule CSV 자동 생성** - 선택된 객체에서 일정 CSV 생성
- **Task 설정** - 이름 접두사, 작업 유형 (Construct/Demolish/Temporary), 기간, 시작일
- **ParentSet 전략** - ByLevel, ByProperty, Custom 지원
- **미리보기 기능** - 생성 전 DataGrid 미리보기
- **AWP 4D 연동** - 생성된 CSV를 AWP 4D 탭에서 TimeLiner에 적용 가능

### New Files
- `ScheduleBuilderViewModel.cs` - Schedule Builder ViewModel
- `SchedulePreviewItem.cs` - 미리보기 아이템 모델

### UI
- **Schedule 탭** 추가 - 우측 패널에 새 탭
- **미리보기 DataGrid** - Task명, 시작일, 종료일, 유형, ParentSet 표시
- **버전 업데이트** - Info 탭 버전 0.8.0으로 업데이트

**→ [Phase 10 Document](docs/phases/phase-10-refined-schedule-builder.md)**

---

## [0.6.0] - 2026-01-11

### AWP 4D Automation (Phase 8)
- **CSV → TimeLiner 자동 연결** 파이프라인 완성
- **Property Write** - ComAPI `SetUserDefined()` 기반 Custom Property 기입
- **Selection Set 자동 생성** - 계층 구조 (ParentSet 경로 기반)
- **TimeLiner Task 자동 생성** - Selection Set 연결 포함
- **AWP 4D 탭** - UI 통합 (Execute, Validate, Dry Run, Cancel, Clear)

### New Services
- `PropertyWriteService.cs` - ComAPI Property Write (재시도 로직)
- `SelectionSetService.cs` - Selection Set 계층 구조 생성
- `TimeLinerService.cs` - TimeLiner Task 생성 및 Set 연결
- `AWP4DAutomationService.cs` - 통합 파이프라인 (이벤트 기반)
- `ObjectMatcher.cs` - SyncID → ModelItem 매칭 (캐싱)
- `AWP4DValidator.cs` - Pre/Post 검증
- `ScheduleCsvParser.cs` - 한영 컬럼 매핑 CSV 파싱

### Models
- `ScheduleData.cs` - 스케줄 데이터 (MatchStatus 포함)
- `AWP4DOptions.cs` - GroupingStrategy, TaskSelectionMode
- `AutomationResult.cs` - 단계별 결과 + LogEntry
- `ValidationResult.cs` - ErrorCode, WarningCode 체계

### Technical
- **Read-Only Collection Bypass**: AddCopy/InsertCopy/TasksCopyFrom 패턴 적용
- **TypeConversion**: SelectionSet → SelectionSource 변환 패턴
- **ADR-002**: TimeLiner API Integration 문서화

**→ [Phase 8 Document](docs/phases/phase-8-awp-4d-automation.md)**

---

## [0.5.0] - 2026-01-09

### Code Quality
- **ViewModel 리팩토링** - 2213줄 DXwindowViewModel을 7개 Partial Class로 분리
  - `DXwindowViewModel.cs` (Core: 1020줄)
  - `DXwindowViewModel.Filter.cs` (144줄)
  - `DXwindowViewModel.Search.cs` (110줄)
  - `DXwindowViewModel.Selection.cs` (219줄)
  - `DXwindowViewModel.Snapshot.cs` (311줄)
  - `DXwindowViewModel.Tree.cs` (181줄)
  - `DXwindowViewModel.Export.cs` (397줄)

### New Features
- **CSV Viewer UI** - 우측 패널에 CSV 뷰어 탭 추가
  - CSV 파일 로드 및 DataGrid 표시
  - 컬럼별 필터링 (전체/특정 컬럼)
  - 필터링된 데이터 CSV Export
  - UTF-8/EUC-KR 인코딩 자동 감지

### Research Completed
- **ComAPI Property Write 가능성 조사** - ✅ 완료
  - .NET API는 Property Read-Only (Write 불가)
  - ComAPI `SetUserDefined()` 메서드로 Custom Property 추가 가능
  - ADR-001 문서 작성 완료

### Bug Fixes
- **버전 정보 불일치** - XAML 버전 1.1.0 → 0.5.0 수정

### Technical
- `CsvViewerViewModel.cs` - CSV 뷰어 전용 ViewModel 신규 생성
- `DXwindow.xaml` - CSV Viewer TabItem 추가
- `docs/adr/ADR-001-ComAPI-Property-Write.md` - 아키텍처 결정 기록

**→ [Sprint v0.5.0](docs/agile/SPRINT-v0.5.0.md)**

---

## [0.4.3] - 2026-01-09

### New Features
- **필터 자동 적용** - 중앙 패널 필터가 DataGrid에 실시간 연동
  - Level, Path, Category, Property, Value 필터 변경 시 자동 적용
  - 200ms 디바운스로 성능 최적화
- **Show Only 토글 버튼** - On/Off 상태 전환 가능
  - ON: 필터링된 객체만 표시 (오렌지색 버튼)
  - OFF: 모든 객체 표시 (파란색 버튼)

### Bug Fixes
- **Save ViewPoint 오류 수정** - "Invalid object" COM API 오류 해결
  - COM API 대신 .NET API `DocumentSavedViewpoints.AddCopy()` 사용
  - `SavedViewpoint` 객체를 현재 뷰에서 직접 생성
  - ViewPoint 저장 안정성 대폭 향상

### Technical
- `DXwindowViewModel` - TriggerFilterDebounce() 메서드 추가
- `DXwindowViewModel` - IsShowOnlyActive, ShowOnlyButtonText, ShowOnlyButtonColor 프로퍼티 추가
- `SnapshotService.SaveCurrentViewPoint()` - .NET API 방식으로 완전 재작성
- `DXwindow.xaml` - Show Only 버튼 동적 스타일 바인딩

---

## [0.4.2] - 2026-01-09

### New Features
- **Unit 컬럼 추가** - 중앙 패널 DataGrid에 Unit 컬럼 표시
  - 추출 시점에 DisplayString 파싱 적용
  - 단위가 있는 데이터에 단위 분리 표시
  - 단위가 없는 데이터는 빈 셀
- **CSV Export Unit 포함** - Hierarchy CSV에 DataType, Unit 컬럼 추가
- **JSON Export Unit 포함** - TreeNode의 PropertyData에 DataType, Unit 필드 추가

### Bug Fixes
- **AccessViolationException 처리** - Navisworks API 내부 오류 안정적 처리
  - `[HandleProcessCorruptedStateExceptions]` 속성 추가
  - Corrupted State Exception을 catch하여 해당 카테고리만 건너뛰고 계속 진행

### Technical
- `HierarchicalPropertyRecord` - DataType, RawValue, NumericValue, Unit 필드 추가
- `NavisworksDataExtractor` - 추출 시점에 DisplayStringParser 사용
- `NavisworksDataExtractor` - HandleProcessCorruptedStateExceptions 속성 추가
- `PropertyItemViewModel` - Unit 프로퍼티 추가
- `HierarchyFileWriter.WriteToCsv()` - includeUnit 파라미터 추가
- `DXwindow.xaml` - DataGrid Unit 컬럼 추가

**→ [Sprint v0.4.2](docs/agile/SPRINT-v0.4.2.md)**

---

## [0.4.1] - 2026-01-08

### Bug Fixes (Critical)
- **트리 계층 구조 수정** - Navisworks와 동일한 완전한 계층 트리 구조
  - 컨테이너 노드(속성 없음)도 트리에 포함
  - ModelItem.Children을 직접 사용하여 정확한 부모-자식 관계 유지
  - 누락되던 중간 레벨(L1, L3, L6, L7 등) 노드 표시

### Technical
- `BuildTreeFromModelItem()` - ModelItem에서 직접 재귀적 트리 구축
- `GetDisplayNameFromModelItem()` - 헬퍼 메서드 추가
- 상태 메시지에 컨테이너 노드 수 표시

**→ [Sprint v0.4.1](docs/agile/SPRINT-v0.4.1.md)**

---

## [0.4.0] - 2026-01-08

### Bug Fixes (P0)
- [x] 검색창 영어 입력 불가능 오류 수정 (IME + PreviewKeyDown 핸들링)
- [x] Save ViewPoint 저장 오류 수정 (COM API 기반 구현)

### New Features
- [x] **4종 CSV 내보내기 버튼** (All/Selection × Properties/Hierarchy)
- [x] **DisplayString 파싱** - VariantData 타입 접두사 파싱 (Refined CSV)
- [x] **관측점 초기화** (Reset to Home) - Home 뷰포인트 또는 Zoom Extents
- [x] **Object 검색 기능** - 이름, 속성값, SysPath로 객체 검색
- [x] **Raw/Refined CSV 동시 저장** - 한 번에 두 형식 내보내기
- [x] **CSV Verbose 로깅** - 내보내기 상세 로그 파일 생성

### Enhancements
- [x] 트리 레벨별 Expand/Collapse (L0~L5 버튼)
- [x] 검색 결과 3D 선택 연동
- [x] SelectByIds / SelectAndZoomByIds API 추가

### Technical
- Services/DisplayStringParser.cs - DisplayString 타입 파싱
- PropertyFileWriter.WriteDualCsv() - 동시 저장
- SnapshotService.ResetToHome() - 관측점 리셋

### Research
- [ ] ComAPI를 통한 외부 Property 기입 가능성 조사

**→ [Sprint v0.4.0](docs/agile/SPRINT-v0.4.0.md)**

---

## [0.3.0] - 2026-01-06

### Added
- **Level-based Tree Expand/Collapse** - L0~L10 레벨별 확장/축소 기능
- Tree expand all / collapse all 버튼
- Level-specific expansion controls

### Changed
- TreeView 성능 개선
- UI 레이아웃 최적화

---

## [0.2.0] - 2026-01-05

### Added
- **3D Object Selection** - 필터링된 객체 Navisworks 선택 연동
- **Visibility Control** - Show Only / Show All 기능
- **Zoom to Selection** - 선택 객체로 카메라 이동
- NavisworksSelectionService.cs - 선택 서비스 모듈

### Changed
- DXwindowViewModel.cs 3D 제어 커맨드 추가

---

## [0.1.0] - 2026-01-03

### Added
- **Level Filter** - 레벨별 속성 필터링 (L0, L1, L2...)
- **SysPath Filter** - 전체 경로 기반 필터링
- **TreeView Hierarchy** - 모델 계층 구조 시각화
- **Visual Level Badges** - 색상 코딩 레벨 표시
- **Node Icons** - 📁 폴더 / 🔷 그룹 / 📄 항목 아이콘

### Technical
- WPF MVVM 아키텍처 구현
- NavisworksDataExtractor.cs - 데이터 추출 서비스
- HierarchyFileWriter.cs - CSV/JSON 내보내기

---

## [0.0.1] - 2025-12-29

### Added
- Initial project setup
- Navisworks 2025 plugin scaffold
- Basic DXwindow.xaml UI
- DX.cs plugin entry point

---

[Unreleased]: https://github.com/tygwan/DXTnavis/compare/v0.5.0...HEAD
[0.5.0]: https://github.com/tygwan/DXTnavis/compare/v0.4.3...v0.5.0
[0.4.3]: https://github.com/tygwan/DXTnavis/compare/v0.4.2...v0.4.3
[0.4.2]: https://github.com/tygwan/DXTnavis/compare/v0.4.1...v0.4.2
[0.4.1]: https://github.com/tygwan/DXTnavis/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/tygwan/DXTnavis/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/tygwan/DXTnavis/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/tygwan/DXTnavis/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/tygwan/DXTnavis/compare/v0.0.1...v0.1.0
[0.0.1]: https://github.com/tygwan/DXTnavis/releases/tag/v0.0.1
