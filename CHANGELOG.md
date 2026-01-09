# Changelog

All notable changes to DXTnavis will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[Unreleased]: https://github.com/tygwan/DXTnavis/compare/v0.4.2...HEAD
[0.4.2]: https://github.com/tygwan/DXTnavis/compare/v0.4.1...v0.4.2
[0.4.1]: https://github.com/tygwan/DXTnavis/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/tygwan/DXTnavis/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/tygwan/DXTnavis/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/tygwan/DXTnavis/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/tygwan/DXTnavis/compare/v0.0.1...v0.1.0
[0.0.1]: https://github.com/tygwan/DXTnavis/releases/tag/v0.0.1
