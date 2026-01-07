# Changelog

All notable changes to DXTnavis will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased] - v0.4.0

### Bug Fixes (P0)
- [x] 검색창 영어 입력 불가능 오류 수정 (IME + PreviewKeyDown 핸들링)
- [x] Save ViewPoint 저장 오류 수정 (COM API 기반 구현)

### New Features
- [ ] 관측점 초기화 기능 (Reset to Home)
- [ ] Object 검색 기능 (이름/속성 기반)
- [ ] Selection 기반 Properties 출력 (All/Sele x Prop/Hier)
- [ ] DisplayString 접두사 파싱 (Refined CSV)
- [ ] Raw/Refined CSV 동시 관리

### Enhancements
- [x] 트리 레벨별 Expand/Collapse (각 레벨 개별 제어) - L0~L5 버튼
- [ ] Level 필터링 UI 명세화
- [ ] CSV 출력 Verbose 로깅

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

[Unreleased]: https://github.com/tygwan/DXTnavis/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/tygwan/DXTnavis/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/tygwan/DXTnavis/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/tygwan/DXTnavis/compare/v0.0.1...v0.1.0
[0.0.1]: https://github.com/tygwan/DXTnavis/releases/tag/v0.0.1
