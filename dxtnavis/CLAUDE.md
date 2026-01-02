# DXTnavis - Navisworks 2025 Property Viewer Plugin

## Project Overview
- **Purpose**: Standalone Navisworks property viewer and CSV exporter plugin
- **Tech Stack**: C# .NET Framework 4.8, WPF, Navisworks API 2025
- **Architecture**: MVVM pattern with modular services

## Critical Version Constraints
**These versions MUST NOT be changed:**
- Navisworks 2025 (locked)
- .NET Framework 4.8 (required by Navisworks 2025)
- Target x64 platform

## Project Structure
```
dxtnavis/
├── Converters/           # WPF value converters
│   └── BoolToVisibilityConverter.cs
├── Helpers/              # MVVM helper classes
│   └── RelayCommand.cs
├── Models/               # Data models
│   ├── HierarchicalPropertyRecord.cs
│   ├── PropertyInfo.cs
│   └── TreeNodeModel.cs
├── Properties/           # Assembly info
│   └── AssemblyInfo.cs
├── Resources/            # Plugin icons
│   ├── icon_16x16.png
│   └── icon_32x32.png
├── Services/             # Business logic services
│   ├── FullModelExporterService.cs
│   ├── HierarchyFileWriter.cs
│   ├── NavisworksDataExtractor.cs
│   ├── PropertyFileWriter.cs
│   └── SetCreationService.cs
├── ViewModels/           # MVVM ViewModels
│   ├── DXwindowViewModel.cs
│   ├── HierarchyNodeViewModel.cs
│   └── PropertyItemViewModel.cs
├── Views/                # WPF Views
│   ├── DXwindow.xaml
│   └── DXwindow.xaml.cs
├── DX.cs                 # Main plugin entry point
├── DXTnavis.csproj       # Project file
└── DXTnavis.sln          # Solution file
```

## Core Features

### 1. Plugin Entry Point (DX.cs)
- `[Plugin("DXTnavis.DX")]` Navisworks AddInPlugin
- Singleton window pattern for UI management
- Plugin lifecycle management

### 2. Property Extraction (NavisworksDataExtractor.cs)
- Recursive hierarchy traversal
- PropertyCategories and DataProperty extraction
- AccessViolationException handling for stable API access

### 3. CSV Export Services
- **FullModelExporterService**: Export all model properties
- **HierarchyFileWriter**: Export hierarchy as CSV/JSON
- **PropertyFileWriter**: Basic property file export

### 4. Search Set Creation (SetCreationService.cs)
- Create Navisworks Search Sets from selected properties
- Folder organization support

### 5. UI (DXwindow.xaml)
- Three-panel layout:
  - Left: Object hierarchy TreeView
  - Center: Property DataGrid with checkboxes
  - Right: Search Set creation / Settings tabs

## Build & Deployment

### Build Requirements
- Visual Studio 2022+
- Navisworks Manage 2025 installed
- .NET Framework 4.8 SDK

### PostBuild Deployment
Automatic deployment to Navisworks plugins folder:
```
C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\DXTnavis\
```

**Note**: Requires Visual Studio to run as Administrator for write access to Program Files.

Files deployed:
- DXTnavis.dll (main plugin)
- Newtonsoft.Json.dll
- System.Text.Json.dll
- System.Text.Encodings.Web.dll
- Microsoft.Bcl.AsyncInterfaces.dll
- System.Buffers.dll
- System.Memory.dll
- System.Numerics.Vectors.dll
- System.Runtime.CompilerServices.Unsafe.dll
- System.Threading.Tasks.Extensions.dll
- System.ValueTuple.dll

## API Dependencies
```xml
<Reference Include="Autodesk.Navisworks.Api"/>
<Reference Include="Autodesk.Navisworks.Automation"/>
<Reference Include="Autodesk.Navisworks.Clash"/>
<Reference Include="Autodesk.Navisworks.ComApi"/>
<Reference Include="Autodesk.Navisworks.Controls"/>
<Reference Include="Autodesk.Navisworks.Timeliner"/>
```

## Development Guidelines

### Navisworks API Thread Safety
- All Navisworks API calls must be on UI thread
- Never use `Task.Run()` with `Application.ActiveDocument`
- Use Dispatcher for cross-thread UI updates

### Error Handling Pattern
```csharp
try
{
    var properties = category.Properties;
}
catch (System.AccessViolationException)
{
    // Log and skip - common with some Navisworks property categories
    continue;
}
```

### MVVM Best Practices
- Use RelayCommand for ICommand implementations
- Use AsyncRelayCommand for async operations
- Implement INotifyPropertyChanged for all bindable properties

## Current Status
- Standalone version with no external dependencies
- CSV/JSON export fully functional
- Search Set creation working
- Settings UI present (but disabled in standalone mode)
- **Phase 1 Complete**: Level/SysPath filtering implemented
- **Phase 3 Complete**: 3D object selection and visibility control

---

## Development Roadmap

### Phase 1: Filtering Enhancement ✅ COMPLETE
- [x] **Level Filter**: ComboBox with (All), L0, L1, L2... options
- [x] **Sys Path Filter**: Full hierarchical path with `>` separator
  - Format: `Project > Building > Level 1 > Wall`
  - Distinguishes same-named objects at different locations
- [ ] **Property Exclusion**: Filter out unnecessary values (DisplayString, etc.)

### Phase 2: UI/Layout Improvements
- [ ] **Vertical Layout Option**: PropertyGrid-style stacked display
- [ ] **Full Path Display**: Show complete hierarchy path in TreeView

### Phase 3: 3D Object Integration ✅ COMPLETE
- [x] **Conditional 3D Display**: Show only filtered objects in Navisworks view
  - Uses `Models.SetHidden()` API for visibility control
  - "Show Only" button hides non-filtered objects
  - "Show All" button resets visibility
- [x] **Object List Selection**: Sync FilteredProperties to Navisworks selection
  - "Select in 3D" button selects filtered/checked objects
  - "Zoom" button zooms camera to selected objects
- [ ] **3D Snapshot** (Future):
  - Image capture (PNG/JPG)
  - ViewPoint save (SavedViewpoint API)
  - Naming: `{FilterCondition}_{Timestamp}.{ext}`

### Phase 4: Data Validation
- [ ] **Unit Mismatch Detection**: Identify inconsistent units across properties
- [ ] **Validation Report**: Generate warnings and reports

### Implemented Services
| Service | Purpose | Status |
|---------|---------|--------|
| `NavisworksSelectionService.cs` | 3D object selection/visibility control | ✅ Implemented |
| `SnapshotService.cs` | Image capture and ViewPoint save | 📋 Planned |

---

## Git Repository
- Remote: https://github.com/tygwan/DXTnavis.git
- Branch: main
