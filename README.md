<div align="center">

# DXTnavis

**Navisworks 2025 Property Viewer & 4D Automation Plugin**

[![Version](https://img.shields.io/badge/Version-0.8.0-blue?style=flat-square)]()
[![Navisworks](https://img.shields.io/badge/Navisworks-2025-FF6D00?style=flat-square&logo=autodesk&logoColor=white)](https://www.autodesk.com/products/navisworks)
[![.NET](https://img.shields.io/badge/.NET_Framework-4.8-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-MVVM-0078D4?style=flat-square&logo=windows&logoColor=white)]()
[![Platform](https://img.shields.io/badge/Platform-x64-green?style=flat-square)]()

<br/>

*BIM 모델의 속성을 효율적으로 관리하고 4D 시뮬레이션을 자동화하는 Navisworks 플러그인*

[Features](#-features) • [Quick Start](#-quick-start) • [Installation](#-installation) • [Changelog](CHANGELOG.md)

---

### Plugin Interface

![DXTnavis Main Page](snapshots/dxtnavis_main_page.png)

</div>

---

## Features

<table>
<tr>
<td align="center" width="12%">
<h3>🌳</h3>
<b>Hierarchy</b><br/>
<sub>Level-based<br/>expand/collapse</sub>
</td>
<td align="center" width="12%">
<h3>🔍</h3>
<b>Search</b><br/>
<sub>Object search<br/>by name/path</sub>
</td>
<td align="center" width="12%">
<h3>🎯</h3>
<b>3D Control</b><br/>
<sub>Select, Show,<br/>Zoom, Reset</sub>
</td>
<td align="center" width="12%">
<h3>📤</h3>
<b>Export</b><br/>
<sub>Raw + Refined<br/>CSV dual export</sub>
</td>
<td align="center" width="12%">
<h3>📸</h3>
<b>Snapshot</b><br/>
<sub>ViewPoint<br/>Save & Reset</sub>
</td>
<td align="center" width="12%">
<h3>📊</h3>
<b>CSV Viewer</b><br/>
<sub>Load, Filter,<br/>Export CSV</sub>
</td>
<td align="center" width="12%">
<h3>🎬</h3>
<b>AWP 4D</b><br/>
<sub>CSV → TimeLiner<br/>Automation</sub>
</td>
<td align="center" width="12%">
<h3>⚡</h3>
<b>Async Load</b><br/>
<sub>Progress bar,<br/>Cancel support</sub>
</td>
</tr>
</table>

### AWP 4D Automation (v0.6.0) 🆕
CSV 스케줄 데이터에서 4D 시뮬레이션 자동 생성 파이프라인

| 단계 | 기능 | 설명 |
|:----:|------|------|
| 1 | **CSV Import** | 한영 컬럼 매핑 지원 (SyncID, 작업명, 계획시작...) |
| 2 | **Object Matching** | SyncID 기반 ModelItem 자동 매칭 |
| 3 | **Property Write** | ComAPI로 Custom Property 기입 |
| 4 | **Selection Set** | 계층적 Selection Set 자동 생성 |
| 5 | **TimeLiner Task** | Task 생성 및 Set 연결 |

> **지원 옵션**: Dry Run, Grouping Strategy, 최소 매칭률, 폴더명 설정

### Hierarchy Navigation
- 모델 전체 계층 구조 TreeView 시각화
- **Level-based Expand/Collapse** - 레벨별 확장/축소 (L0~L10)
- **Visual Indicators** - 색상 배지, 노드 아이콘, 하위 개수

### Property Viewer
- 실시간 속성 표시 (Category → Property → Value)
- **Level Filter** - L0, L1, L2... 레벨별 필터링
- **Sys Path Filter** - 전체 경로 기반 필터링

### 3D Object Control
| 버튼 | 기능 |
|------|------|
| `Select in 3D` | 필터링된 객체를 Navisworks에서 선택 |
| `Show Only` | 필터링된 객체만 표시 (나머지 숨김) |
| `Show All` | 전체 객체 표시 복원 |
| `Zoom` | 선택된 객체로 카메라 이동 |
| `Reset Home` | 초기 뷰포인트로 리셋 |

### Object Search
- 객체 이름, 속성값, SysPath로 검색
- 검색 결과 자동 3D 선택 연동
- 검색 결과로 Zoom 기능

### CSV Export
| 버튼 | 설명 |
|------|------|
| `All Properties` | 전체 모델 속성 내보내기 |
| `All Hierarchy` | 전체 계층 구조 내보내기 |
| `Selection Properties` | 선택 객체 속성 내보내기 |
| `Selection Hierarchy` | 선택 객체 계층 내보내기 |

> Raw CSV + Refined CSV (DisplayString 파싱) 동시 저장

### CSV Viewer
- 외부 CSV 파일 로드 및 DataGrid 표시
- **컬럼별 필터링** - 전체 컬럼 또는 특정 컬럼 검색
- **필터링된 데이터 Export** - 필터 결과를 새 CSV로 저장
- **인코딩 자동 감지** - UTF-8, EUC-KR 지원

---

## Quick Start

```
1. Visual Studio에서 DXTnavis.sln 열고 빌드 (관리자 권한)
2. Navisworks 2025 실행 → Home 탭 → DXTnavis 클릭
3. 계층 구조 로드 → 필터링 → 3D 제어
4. AWP 4D 탭에서 스케줄 CSV 로드 → Execute
```

---

## Installation

### Requirements

| 항목 | 버전 |
|------|------|
| Visual Studio | 2022+ |
| .NET Framework | 4.8 |
| Navisworks Manage | 2025 |

### Build & Deploy

```bash
# 빌드 (관리자 권한 필요)
dotnet build DXTnavis.csproj -c Debug
```

> 빌드 후 자동 배포: `C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\`

---

## Development Status

```
v0.6.0: [====================] 100% ✅ Released 2026-01-11
v0.7.0: [====================] 100% ✅ Released 2026-01-13
v0.8.0: [====================] 100% ✅ Released 2026-01-13
```

| Phase | Feature | Status |
|:-----:|---------|:------:|
| 1 | Property Filtering | ✅ 100% |
| 2 | UI Enhancement | ✅ 100% |
| 3 | 3D Integration | ✅ 100% |
| 4 | CSV Enhancement | ✅ 100% |
| 5 | Data Validation | ✅ 100% |
| 6 | Code Quality | ✅ 100% |
| 7 | CSV Viewer | ✅ 100% |
| 8 | AWP 4D Automation | ✅ 100% |
| 9 | UI Enhancement v2 | ✅ 100% |
| 10 | Load Optimization | ✅ 100% |

**→ [Changelog](CHANGELOG.md)**

### Release History

| Version | Features | Date |
|:-------:|----------|:----:|
| **v0.8.0** | **Async Loading, Progress UI, Cancellation, Single-Pass Optimization** | 2026-01-13 |
| v0.7.0 | Data Validation, Grouped Property View, Select All | 2026-01-13 |
| v0.6.0 | AWP 4D Automation Pipeline | 2026-01-11 |
| v0.5.0 | ViewModel Refactoring, CSV Viewer, ComAPI Research | 2026-01-09 |
| v0.4.x | Auto Filter, Show Only Toggle, Unit Column | 2026-01-09 |
| v0.4.0 | Object Search, 4종 CSV, Reset Home, Dual Export | 2026-01-08 |
| v0.3.0 | Tree Expand/Collapse, Level Badges | 2026-01-06 |
| v0.2.0 | 3D Selection, Visibility Control, Zoom | 2026-01-05 |
| v0.1.0 | Level Filter, SysPath Filter, TreeView | 2026-01-03 |

### v0.8.0 주요 기능 (Released) 🆕

| Category | Feature | Status |
|:--------:|---------|:------:|
| ⚡ Perf | 비동기 로딩 (UI 프리징 제거) | ✅ Complete |
| ⚡ Perf | ProgressBar + 상태 텍스트 | ✅ Complete |
| ⚡ Perf | 취소 버튼 (즉시 중단) | ✅ Complete |
| ⚡ Perf | 단일 순회 최적화 (2배 성능) | ✅ Complete |

**구현된 UI**:
```
┌─────────────────────────────────────────────────────────────┐
│  [📂 Loading...] [⏹ Cancel]                                 │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ ████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ 35%  │  │
│  └───────────────────────────────────────────────────────┘  │
│  Building tree: 3,500 / 10,000 (35%)                        │
└─────────────────────────────────────────────────────────────┘
```

### v0.7.0 주요 기능

| Category | Feature | Status |
|:--------:|---------|:------:|
| ✅ UI | Select All 체크박스 (전체 선택/해제) | ✅ Complete |
| ✅ UI | 객체별 그룹화 표시 (Expander) | ✅ Complete |
| ✅ UI | 카테고리별 하위 그룹화 | ✅ Complete |
| ✅ UI | Expand/Collapse All 버튼 | ✅ Complete |
| ✅ Validation | 단위 불일치 감지 | ✅ Complete |
| ✅ Validation | 필수 속성 누락 확인 | ✅ Complete |

### v0.6.0 주요 변경

| Category | Feature | Status |
|:--------:|---------|:------:|
| 🆕 AWP 4D | CSV → Property Write → Selection Set → TimeLiner | ✅ |
| 🆕 AWP 4D | SyncID 기반 ModelItem 자동 매칭 | ✅ |
| 🆕 AWP 4D | ComAPI Property Write 구현 | ✅ |
| 🆕 AWP 4D | 한영 컬럼 매핑 CSV 파서 | ✅ |
| 🆕 AWP 4D | Dry Run / Validation 모드 | ✅ |

---

## Project Structure

<details>
<summary><b>Click to expand</b></summary>

```
dxtnavis/
├── Models/
│   ├── HierarchicalPropertyRecord.cs
│   ├── TreeNodeModel.cs
│   ├── PropertyInfo.cs
│   ├── ScheduleData.cs          # v0.6.0 - 스케줄 데이터
│   ├── AWP4DOptions.cs          # v0.6.0 - 파이프라인 옵션
│   ├── AutomationResult.cs      # v0.6.0 - 실행 결과
│   ├── ValidationResult.cs      # v0.6.0 - 검증 결과
│   └── LoadProgress.cs          # v0.8.0 - 로딩 진행률 모델
├── ViewModels/                   # MVVM (Partial Class 패턴)
│   ├── DXwindowViewModel.cs      # Core
│   ├── DXwindowViewModel.*.cs    # Partial classes
│   ├── AWP4DViewModel.cs         # v0.6.0 - AWP 4D ViewModel
│   ├── CsvViewerViewModel.cs
│   ├── PropertyItemViewModel.cs  # v0.7.0 - 속성 그룹화 VM
│   └── HierarchyNodeViewModel.cs
├── Views/
│   └── DXwindow.xaml             # 메인 UI + AWP 4D 탭
├── Services/
│   ├── NavisworksDataExtractor.cs
│   ├── NavisworksSelectionService.cs
│   ├── PropertyWriteService.cs   # v0.6.0 - ComAPI Property Write
│   ├── SelectionSetService.cs    # v0.6.0 - Selection Set 생성
│   ├── TimeLinerService.cs       # v0.6.0 - TimeLiner Task 생성
│   ├── AWP4DAutomationService.cs # v0.6.0 - 통합 파이프라인
│   ├── ObjectMatcher.cs          # v0.6.0 - SyncID 매칭
│   ├── AWP4DValidator.cs         # v0.6.0 - 검증 서비스
│   ├── ScheduleCsvParser.cs      # v0.6.0 - 스케줄 CSV 파서
│   ├── ValidationService.cs      # v0.7.0 - 속성 검증 서비스
│   └── LoadHierarchyService.cs   # v0.8.0 - 최적화된 로딩 서비스
├── snapshots/
│   └── dxtnavis_main_page.png
├── docs/
│   ├── phases/
│   │   ├── phase-5-data-validation.md   # v0.7.0
│   │   ├── phase-8-awp-4d-automation.md
│   │   ├── phase-9-ui-enhancement.md    # v0.7.0
│   │   └── phase-10-load-optimization.md # v0.8.0
│   └── tech-specs/
│       └── AWP-4D-Automation-Spec.md
├── CHANGELOG.md
└── DX.cs (Plugin Entry Point)
```

</details>

---

## Technical Highlights

### AWP 4D Automation Architecture

```
┌─────────────┐    ┌──────────────┐    ┌─────────────────┐
│  CSV File   │───▶│ Schedule     │───▶│ Object Matcher  │
│ (Schedule)  │    │ Parser       │    │ (SyncID → Item) │
└─────────────┘    └──────────────┘    └────────┬────────┘
                                                │
    ┌───────────────────────────────────────────┼───────────────────────────────┐
    │                                           ▼                               │
    │  ┌─────────────────┐    ┌─────────────────────┐    ┌──────────────────┐  │
    │  │ Property Write  │    │ Selection Set       │    │ TimeLiner Task   │  │
    │  │ (ComAPI)        │    │ Creation            │    │ Creation         │  │
    │  └─────────────────┘    └─────────────────────┘    └──────────────────┘  │
    │                                                                           │
    └───────────────────────── AWP4DAutomationService ──────────────────────────┘
```

### API Usage Strategy

| 기능 | API | 이유 |
|------|-----|------|
| Property Write | ComAPI | .NET API는 Read-Only |
| Selection Set | .NET API | AddCopy/InsertCopy 메서드 |
| TimeLiner Task | .NET API | TasksCopyFrom 메서드 |

---

## Guidelines

<details>
<summary><b>Thread Safety</b></summary>

```csharp
// ✅ Navisworks API는 반드시 UI 스레드에서 호출
// ❌ Task.Run() 내에서 Application.ActiveDocument 접근 금지
```

</details>

<details>
<summary><b>ComAPI Property Write</b></summary>

```csharp
InwOpState10 comState = ComApiBridge.State;
InwOaPath comPath = ComApiBridge.ToInwOaPath(modelItem);
InwGUIPropertyNode2 propNode = (InwGUIPropertyNode2)comState.GetGUIPropertyNode(comPath, true);
propNode.SetUserDefined(0, "CategoryName", "InternalName", propVec);
```

</details>

---

## API Dependencies

```xml
<Reference Include="Autodesk.Navisworks.Api"/>
<Reference Include="Autodesk.Navisworks.Automation"/>
<Reference Include="Autodesk.Navisworks.ComApi"/>
<Reference Include="Autodesk.Navisworks.Timeliner"/>
<Reference Include="Autodesk.Navisworks.Interop.ComApi"/>
```

---

<div align="center">

## Author

**Developer** - Yoon Taegwan
**AI Assistant** - Claude (Anthropic)

---

<sub>Last Updated: 2026-01-13 • v0.8.0</sub>

</div>
