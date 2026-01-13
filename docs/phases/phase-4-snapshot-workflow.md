# Phase 4: 3D Snapshot Workflow

> **Status:** ✅ Complete (95%)
> **Parent:** [_INDEX](../_INDEX.md) | **Prev:** [Phase 3](phase-3-3d-integration.md) | **Next:** [Phase 5](phase-5-data-validation.md)
> **Last Updated:** 2026-01-13

## Overview
3D 뷰포트 이미지 캡처 및 ViewPoint 저장 워크플로우

## Requirements (FR-401~406)
| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-401 | 객체 중심 카메라 이동 (ZoomToSelection) | P0 | ✅ |
| FR-402 | 객체 격리 (Isolate) | P0 | ✅ |
| FR-403 | 화면 캡처 및 이미지 저장 | P0 | ✅ |
| FR-404 | 배치 처리 (다중 객체 연속 캡처) | P1 | ✅ |
| FR-405 | 캡처 설정 (해상도, 형식, 배경색) | P1 | ✅ |
| FR-406 | 캡처 이미지와 메타데이터 연결 | P0 | ✅ |

## Implementation

### Key Files
- `Services/SnapshotService.cs` - Core snapshot logic (~400 lines)
- `ViewModels/DXwindowViewModel.cs:1351-1618` - Snapshot commands

### Key Methods
```csharp
// SnapshotService.cs
string CaptureCurrentView(string outputPath, string fileName)
SavedViewpoint SaveCurrentViewPoint(string name, string folder)
string IsolateAndCapture(Guid objectId, string outputPath)
List<string> BatchCaptureFilteredObjects(IEnumerable<HierarchicalPropertyRecord> records, string outputPath, bool isolateEach)
SnapshotResult CaptureWithViewPoint(string filterCondition, string outputPath)
```

### COM API Usage
```csharp
// Image export via DriveIOPlugin
var options = CreateExportImageOptions(width, height, antiAliasLevel, format);
comState.DriveIOPlugin("lcodpimage", fullPath, options);

// Option properties
- export.image.format: lcodpexpng | lcodpexjpg | lcodpexbmp
- export.image.width: 1920
- export.image.height: 1080
- export.image.anti-alias.level: 4
```

## UI Buttons
| Button | Command | Description |
|--------|---------|-------------|
| 📷 Capture View | `CaptureViewCommand` | 현재 뷰 캡처 |
| 📍 Save ViewPoint | `SaveViewPointCommand` | ViewPoint 저장 |
| 📸 Capture + ViewPoint | `CaptureWithViewPointCommand` | 둘 다 저장 |
| 🎬 Batch Capture | `BatchCaptureCommand` | 체크된 객체 배치 캡처 |

## Output
```
📁 output_folder/
├── 📷 Snapshot_20260106_120000.png
├── 📷 Object_guid-xxx.png
└── 📷 Object_guid-yyy.png
```

## Known Issues (Resolved/Documented)

| Issue | Priority | Status | Description |
|-------|----------|--------|-------------|
| ViewPoint 저장 read-only | ✅ Resolved | Fixed | AddCopy() 메서드 사용으로 해결 |
| COM API GUI context | 🟡 Low | Documented | Navisworks GUI 컨텍스트 필요 |
| Anti-aliasing 성능 | 🟡 Low | Documented | Level > 4 시 성능 저하 |

---

## v0.4.0+ Implemented

### Bug Fixes
| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-407 | ViewPoint 저장 read-only 오류 수정 | P0 | ✅ AddCopy() 적용 |
| FR-408 | ComAPI를 통한 ViewPoint 저장 | P0 | ✅ InsertCopy() 폴더 지원 |

### New Features
| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-409 | Home ViewPoint 자동 저장 | P2 | ✅ FindHomeViewpoint() |
| FR-410 | Reset to Home 기능 | P2 | ✅ ResetToHome() |

---

## Completion History
- 2026-01-06: Initial snapshot capture implementation
- 2026-01-06: COM API image export fix
- 2026-01-08: ViewPoint save issue identified (v0.4.0 target)

---

## References
- [Sprint v0.4.0](../agile/SPRINT-v0.4.0.md)
- [Tech Spec v0.4.0](../tech-specs/v0.4.0-tech-spec.md)
