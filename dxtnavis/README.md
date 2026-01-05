# DXTnavis

**Navisworks 2025 Property Viewer & Manager Plugin**

Navisworks Manage 2025용 WPF 기반 속성 뷰어, 계층 구조 탐색, 3D 객체 제어 플러그인

---

## Overview

DXTnavis는 BIM 모델의 속성을 효율적으로 확인하고 관리하기 위한 Navisworks 애드인입니다.

| 항목 | 내용 |
|------|------|
| Platform | Navisworks Manage 2025 |
| Framework | .NET Framework 4.8 |
| Architecture | MVVM (WPF) |
| Target | x64 |

---

## Quick Start

1. **빌드**: Visual Studio에서 `DXTnavis.sln` 열고 빌드 (관리자 권한)
2. **실행**: Navisworks 2025 실행 → Home 탭 → DXTnavis 클릭
3. **사용**:
   - `계층 구조 로드` → TreeView에서 모델 탐색
   - Level/SysPath 필터로 원하는 객체 필터링
   - `Select in 3D` / `Show Only`로 3D 뷰 제어

---

## Features

### Hierarchy Navigation
- 모델 전체 계층 구조 TreeView 시각화
- **Level-based Expand/Collapse** - 레벨별 확장/축소 컨트롤
- **Visual Indicators** - 레벨별 색상 배지, 노드 아이콘, 하위 개수 표시
- 체크박스 기반 노드 선택

### Property Viewer
- 실시간 속성 표시 (Category, Property, Value)
- 읽기/쓰기 권한 상태 표시
- **Level Filter** - L0, L1, L2... 레벨별 필터링
- **Sys Path Filter** - 전체 경로 기반 필터링

### 3D Object Control
- **Select in 3D** - 필터링된 객체를 Navisworks에서 선택
- **Show Only** - 필터링된 객체만 표시 (나머지 숨김)
- **Show All** - 전체 객체 표시 복원
- **Zoom** - 선택된 객체로 카메라 이동

### Export
- CSV Export (Flat)
- JSON Export (Flat / Tree)
- Full Model Export

### Search Set
- 선택된 속성으로 검색 세트 자동 생성
- 폴더 구조 지원

---

## Project Structure

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
│   ├── SetCreationService.cs
│   ├── FullModelExporterService.cs
│   └── PropertyFileWriter.cs
├── Helpers/
│   └── RelayCommand.cs
├── Converters/
│   └── BoolToVisibilityConverter.cs
├── Resources/
│   ├── icon_16x16.png
│   └── icon_32x32.png
└── DX.cs                    # Plugin Entry Point
```

---

## Build & Deploy

### Requirements
- Visual Studio 2022+
- .NET Framework 4.8 SDK
- Navisworks Manage 2025

### Build
```bash
dotnet build DXTnavis.csproj -c Debug
```

### Deploy
빌드 후 자동 배포 (PostBuild Event):
```
C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\
```

> **Note**: Program Files 쓰기 권한 필요 (관리자 권한 실행)

---

## Development Status

### Completed

| Phase | Feature | Status |
|-------|---------|--------|
| 1 | Level Filter | ✅ |
| 1 | Sys Path Filter | ✅ |
| 2 | Tree Expand/Collapse | ✅ |
| 2 | Visual Level Badges | ✅ |
| 3 | 3D Object Selection | ✅ |
| 3 | Visibility Control | ✅ |
| 3 | Zoom to Selection | ✅ |

### Planned

| Phase | Feature | Status |
|-------|---------|--------|
| 2 | Vertical Layout Option | 📋 |
| 3 | 3D Snapshot (PNG/ViewPoint) | 📋 |
| 4 | Unit Mismatch Detection | 📋 |

---

## API Dependencies

```xml
<Reference Include="Autodesk.Navisworks.Api"/>
<Reference Include="Autodesk.Navisworks.Automation"/>
<Reference Include="Autodesk.Navisworks.Clash"/>
<Reference Include="Autodesk.Navisworks.ComApi"/>
<Reference Include="Autodesk.Navisworks.Controls"/>
<Reference Include="Autodesk.Navisworks.Timeliner"/>
```

---

## Guidelines

### Thread Safety
```csharp
// Navisworks API는 반드시 UI 스레드에서 호출
// Task.Run() 내에서 Application.ActiveDocument 접근 금지
```

### Error Handling
```csharp
try {
    var properties = category.Properties;
} catch (AccessViolationException) {
    // 일부 PropertyCategory에서 발생 - skip and continue
}
```

---

## License

Internal Development Project

## Author

- **Developer**: Yoon Taegwan
- **AI Assistant**: Claude (Anthropic)

---

*Last Updated: 2026-01-05*
