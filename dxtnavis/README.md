<div align="center">

# DXTnavis

**Navisworks 2025 Property Viewer & Manager Plugin**

[![Version](https://img.shields.io/badge/Version-0.4.0-blue?style=flat-square)]()
[![Navisworks](https://img.shields.io/badge/Navisworks-2025-FF6D00?style=flat-square&logo=autodesk&logoColor=white)](https://www.autodesk.com/products/navisworks)
[![.NET](https://img.shields.io/badge/.NET_Framework-4.8-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-MVVM-0078D4?style=flat-square&logo=windows&logoColor=white)]()
[![Platform](https://img.shields.io/badge/Platform-x64-green?style=flat-square)]()

<br/>

*BIM 모델의 속성을 효율적으로 확인하고 관리하기 위한 Navisworks 애드인*

[Features](#-features) • [Quick Start](#-quick-start) • [Installation](#-installation) • [Changelog](CHANGELOG.md)

</div>

---

## Features

<table>
<tr>
<td align="center" width="20%">
<h3>🌳</h3>
<b>Hierarchy</b><br/>
<sub>Level-based<br/>expand/collapse</sub>
</td>
<td align="center" width="20%">
<h3>🔍</h3>
<b>Search</b><br/>
<sub>Object search<br/>by name/path</sub>
</td>
<td align="center" width="20%">
<h3>🎯</h3>
<b>3D Control</b><br/>
<sub>Select, Show,<br/>Zoom, Reset</sub>
</td>
<td align="center" width="20%">
<h3>📤</h3>
<b>Export</b><br/>
<sub>Raw + Refined<br/>CSV dual export</sub>
</td>
<td align="center" width="20%">
<h3>📸</h3>
<b>Snapshot</b><br/>
<sub>ViewPoint<br/>Save & Reset</sub>
</td>
</tr>
</table>

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
| `Reset Home` | 초기 뷰포인트로 리셋 (v0.4.0) |

### Object Search (v0.4.0)
- 객체 이름, 속성값, SysPath로 검색
- 검색 결과 자동 3D 선택 연동
- 검색 결과로 Zoom 기능

### CSV Export (v0.4.0)
| 버튼 | 설명 |
|------|------|
| `All Properties` | 전체 모델 속성 내보내기 |
| `All Hierarchy` | 전체 계층 구조 내보내기 |
| `Selection Properties` | 선택 객체 속성 내보내기 |
| `Selection Hierarchy` | 선택 객체 계층 내보내기 |

> Raw CSV + Refined CSV (DisplayString 파싱) 동시 저장

---

## Quick Start

```
1. Visual Studio에서 DXTnavis.sln 열고 빌드 (관리자 권한)
2. Navisworks 2025 실행 → Home 탭 → DXTnavis 클릭
3. 계층 구조 로드 → 필터링 → 3D 제어
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
v0.4.0: [====================] 100% ✅ Released 2026-01-08
```

| Phase | Feature | Status |
|:-----:|---------|:------:|
| 1 | Property Filtering | ✅ 100% |
| 2 | UI Enhancement | ✅ 100% |
| 3 | 3D Integration | ✅ 100% |
| 4 | CSV Enhancement | ✅ 100% |
| 5 | ComAPI Research | 📋 Planned |

**→ [Changelog](CHANGELOG.md)**

### Release History

| Version | Features | Date |
|:-------:|----------|:----:|
| v0.4.0 | Object Search, 4종 CSV, Reset Home, Dual Export, Verbose Log | 2026-01-08 |
| v0.3.0 | Tree Expand/Collapse, Level Badges | 2026-01-06 |
| v0.2.0 | 3D Selection, Visibility Control, Zoom | 2026-01-05 |
| v0.1.0 | Level Filter, SysPath Filter, TreeView | 2026-01-03 |

### v0.4.0 완료 기능

| Priority | Feature | Status |
|:--------:|---------|:------:|
| 🔴 P0 | 검색창 영어 입력 버그 수정 | ✅ |
| 🔴 P0 | ViewPoint 저장 오류 수정 | ✅ |
| 🟠 P1 | 4종 CSV 내보내기 버튼 | ✅ |
| 🟠 P1 | DisplayString 파싱 (Refined CSV) | ✅ |
| 🟡 P2 | 관측점 초기화 (Reset Home) | ✅ |
| 🟡 P2 | Object 검색 기능 | ✅ |
| 🟡 P2 | Raw/Refined CSV 동시 저장 | ✅ |
| 🟢 P3 | CSV Verbose 로깅 | ✅ |

---

## Project Structure

<details>
<summary><b>Click to expand</b></summary>

```
dxtnavis/
├── Models/
│   ├── HierarchicalPropertyRecord.cs
│   ├── TreeNodeModel.cs
│   └── PropertyInfo.cs
├── ViewModels/
│   ├── DXwindowViewModel.cs
│   ├── HierarchyNodeViewModel.cs
│   └── PropertyItemViewModel.cs
├── Views/
│   ├── DXwindow.xaml
│   └── DXwindow.xaml.cs
├── Services/
│   ├── NavisworksDataExtractor.cs
│   ├── NavisworksSelectionService.cs
│   ├── DisplayStringParser.cs      # v0.4.0: VariantData 파싱
│   ├── SnapshotService.cs          # ViewPoint 저장/리셋
│   ├── HierarchyFileWriter.cs
│   └── PropertyFileWriter.cs       # Dual CSV + Verbose Log
├── Helpers/
│   └── RelayCommand.cs
├── Converters/
│   └── BoolToVisibilityConverter.cs
├── docs/
│   └── agile/
│       └── SPRINT-v0.4.0.md
├── CHANGELOG.md
└── DX.cs (Plugin Entry Point)
```

</details>

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
<summary><b>Error Handling</b></summary>

```csharp
try {
    var properties = category.Properties;
} catch (AccessViolationException) {
    // 일부 PropertyCategory에서 발생 - skip and continue
}
```

</details>

---

## API Dependencies

```xml
<Reference Include="Autodesk.Navisworks.Api"/>
<Reference Include="Autodesk.Navisworks.Automation"/>
<Reference Include="Autodesk.Navisworks.ComApi"/>
<Reference Include="Autodesk.Navisworks.Controls"/>
```

---

<div align="center">

## Author

**Developer** - Yoon Taegwan
**AI Assistant** - Claude (Anthropic)

---

<sub>Last Updated: 2026-01-08 • v0.4.0</sub>

</div>
