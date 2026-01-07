<div align="center">

# DXTnavis

**Navisworks 2025 Property Viewer & Manager Plugin**

[![Version](https://img.shields.io/badge/Version-0.3.0-blue?style=flat-square)]()
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
<td align="center" width="25%">
<h3>🌳</h3>
<b>Hierarchy</b><br/>
<sub>Level-based<br/>expand/collapse</sub>
</td>
<td align="center" width="25%">
<h3>🔍</h3>
<b>Filtering</b><br/>
<sub>Level & SysPath<br/>filters</sub>
</td>
<td align="center" width="25%">
<h3>🎯</h3>
<b>3D Control</b><br/>
<sub>Select, Show,<br/>Zoom</sub>
</td>
<td align="center" width="25%">
<h3>📤</h3>
<b>Export</b><br/>
<sub>CSV, JSON<br/>formats</sub>
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
Progress: [==================>     ] 75%
```

| Phase | Feature | Status |
|:-----:|---------|:------:|
| 1 | Property Filtering | ✅ 100% |
| 2 | UI Enhancement | ⚠️ 70% |
| 3 | 3D Integration | ✅ 100% |
| 4 | 3D Snapshot | 📋 Planned |
| 5 | Data Validation | 📋 Planned |

**→ [Sprint Status](docs/agile/SPRINT-CURRENT.md)** | **→ [Changelog](CHANGELOG.md)**

### Completed (v0.3.0)

| Phase | Feature | Description |
|:-----:|---------|-------------|
| 1 | Level Filter | 레벨별 속성 필터링 |
| 1 | Sys Path Filter | 경로 기반 필터링 |
| 2 | Tree Expand/Collapse | 레벨별 확장/축소 |
| 2 | Visual Level Badges | 색상 코딩 레벨 표시 |
| 3 | 3D Object Selection | Navisworks 선택 연동 |
| 3 | Visibility Control | 객체 표시/숨김 |
| 3 | Zoom to Selection | 카메라 이동 |

### Planned

| Phase | Feature |
|:-----:|---------|
| 2 | Vertical Layout Option |
| 4 | PNG Snapshot / ViewPoint |
| 5 | Unit Mismatch Detection |

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
│   ├── HierarchyFileWriter.cs
│   └── PropertyFileWriter.cs
├── Helpers/
│   └── RelayCommand.cs
├── Converters/
│   └── BoolToVisibilityConverter.cs
├── docs/
│   └── agile/
│       └── SPRINT-CURRENT.md
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

<sub>Last Updated: 2026-01-07 • v0.3.0</sub>

</div>
