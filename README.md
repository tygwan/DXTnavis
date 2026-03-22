<div align="center">
  <img src=".github/thumbnail.png" alt="DXTnavis" width="600" />

  <h1>DXTnavis</h1>
  <p><strong>Navisworks BIM Data Extraction, 3D Mesh Export & 4D Automation Plugin</strong></p>

  <p>
    <img src="https://img.shields.io/badge/version-1.6.0-022448" alt="Version" />
    <img src="https://img.shields.io/badge/status-production-10b981" alt="Status" />
    <img src="https://img.shields.io/badge/C%23-WPF-022448" alt="C# WPF" />
    <img src="https://img.shields.io/badge/Navisworks-2025-3b82f6" alt="Navisworks" />
    <img src="https://img.shields.io/badge/.NET_Framework-4.8-022448" alt=".NET" />
    <img src="https://img.shields.io/badge/glTF_2.0-GLB_Export-3b82f6" alt="GLB" />
  </p>
</div>

---

## Overview

DXTnavis is a Navisworks 2025 plugin for extracting BIM properties, geometry, and 3D meshes, plus automating 4D construction simulations. It provides a 5-stage full pipeline export (hierarchy CSV, bounding-box geometry, per-object GLB meshes, spatial adjacency with RDF/TTL, and a 22-column unified CSV), an AWP 4D automation pipeline that transforms schedule CSVs into TimeLiner simulations, and a complete BIM data management suite with hierarchy navigation, filtering, and 3D viewport control.

## Key Features

- **BIM Property Management** -- Hierarchy navigation (L0-L10), real-time property filtering, object grouping (445K to ~5K), dual CSV export (Raw + Refined)
- **AWP 4D Automation** -- End-to-end pipeline: CSV Import > SyncID Matching > ComAPI Property Write > Selection Set > TimeLiner Task
- **Direct TimeLiner** -- One-click TimeLiner connection without CSV, reducing 7 steps to 3 (57% faster)
- **3D Mesh GLB Export** -- COM API-based glTF 2.0 binary mesh extraction with LCS-to-WCS coordinate transformation
- **Geometry & Spatial Analysis** -- BBox/Centroid extraction, adjacency detection via Union-Find, RDF/TTL triple generation
- **Unified CSV Export** -- 22-column single-row-per-object schema for knowledge graph integration
- **3D Viewport Control** -- Select, Show Only, Show All, Zoom, and Reset Home from filtered results

## Tech Stack

| Category | Technologies |
|----------|-------------|
| Language | C# (.NET Framework 4.8) |
| UI | WPF (XAML + MVVM, Partial Class pattern) |
| Platform | Navisworks Manage 2025 (x64) |
| APIs | Navisworks .NET API, ComAPI, TimeLiner API |
| Output Formats | CSV, JSON, GLB (glTF 2.0), RDF/TTL |

## Architecture

### Full Pipeline (5-Stage Export)

```
┌─────────┬─────────┬─────────┬──────────┬──────────────────┐
│ Stage 1 │ Stage 2 │ Stage 3 │ Stage 4  │ Stage 5          │
│Hierarchy│Geometry │  Mesh   │ Spatial  │ Unified CSV      │
│  CSV    │BBox+CSV │  GLB    │Adjacency │ 22-col Schema    │
│         │manifest │per-obj  │ RDF/TTL  │ 1row=1obj        │
└─────────┴─────────┴─────────┴──────────┴──────────────────┘
```

### Hybrid API Strategy

| Feature | API | Reason |
|---------|-----|--------|
| Property Read | .NET API | Standard data access |
| **Property Write** | **ComAPI** | .NET API is Read-Only |
| Selection Set | .NET API | AddCopy/InsertCopy methods |
| TimeLiner Task | .NET API | TasksCopyFrom method |
| **Mesh Extract** | **ComAPI** | GenerateSimplePrimitives() |
| **ViewPoint Save** | **ComAPI** | Not supported in .NET API |

## Getting Started

### Requirements

| Component | Version |
|-----------|---------|
| Visual Studio | 2022+ |
| .NET Framework | 4.8 |
| Navisworks Manage | 2025 |
| Platform | x64 |

### Build & Deploy

```bash
# Build (administrator privileges required)
MSBuild DXTnavis.csproj /p:Configuration=Release /p:Platform=x64
```

> After build, the plugin auto-deploys to: `C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\`

### Usage

1. Open `DXTnavis.sln` in Visual Studio and build
2. Launch Navisworks 2025 > Home tab > DXTnavis
3. Browse hierarchy > Filter properties > Control 3D view
4. For 4D simulation: AWP 4D tab > Load schedule CSV > Execute
5. Full Pipeline for Geometry + Mesh + Spatial integrated export

## Release History

| Version | Key Feature | Date |
|:-------:|-------------|:----:|
| **v1.6.0** | **3D Mesh GLB Export (glTF 2.0)** | 2026-02-14 |
| v1.5.0 | Unified CSV + Spatial Connectivity | 2026-02-10 |
| v1.4.0 | Geometry Export (BBox/Centroid/RDF) | 2026-02-06 |
| v1.3.0 | Synthetic ID Generation | 2026-02-05 |
| v1.2.0 | Direct TimeLiner Execution | 2026-01-21 |
| v1.1.0 | TimeLiner Enhancement | 2026-01-21 |
| v1.0.0 | Grouped Data Structure (445K to 5K) | 2026-01-20 |
| v0.9.0 | Object Grouping MVP | 2026-01-20 |
| v0.8.0 | Schedule Builder | 2026-01-19 |
| v0.6.0 | AWP 4D Automation Pipeline | 2026-01-11 |
| v0.5.0 | ViewModel Refactoring, CSV Viewer | 2026-01-09 |
| v0.4.0 | Object Search, Dual CSV Export | 2026-01-08 |
| v0.3.0 | Tree Expand/Collapse | 2026-01-06 |
| v0.2.0 | 3D Selection, Visibility, Zoom | 2026-01-05 |
| v0.1.0 | Level Filter, SysPath Filter, TreeView | 2026-01-03 |

**[Full Changelog](CHANGELOG.md)**

## Project Structure

```
DXTnavis/
├── Services/
│   ├── NavisworksDataExtractor.cs        # Property extraction + Synthetic ID
│   ├── NavisworksSelectionService.cs     # 3D selection/visibility control
│   ├── PropertyWriteService.cs           # ComAPI Property Write
│   ├── AWP4DAutomationService.cs         # Integrated automation pipeline
│   ├── UnifiedCsvExporter.cs             # 22-col unified CSV
│   ├── Geometry/
│   │   ├── GeometryExtractor.cs          # BBox extraction
│   │   ├── MeshExtractor.cs              # COM API GLB mesh export
│   │   └── GeometryRdfIntegrator.cs      # RDF/TTL conversion
│   └── Spatial/
│       ├── AdjacencyDetector.cs           # BBox adjacency detection
│       ├── ConnectedComponentFinder.cs    # Union-Find groups
│       └── SpatialRelationshipWriter.cs   # adjacency.csv + TTL
├── ViewModels/                            # MVVM Partial Class Pattern
├── Models/                                # Data models (geometry, spatial, schedule)
├── Views/
│   └── DXwindow.xaml                     # Main UI (5 Tabs)
└── docs/
```

## Output Formats

| Format | Content | Consumer |
|--------|---------|----------|
| `hierarchy.csv` | Model hierarchy | Excel, Python |
| `geometry.csv` | BBox + Centroid | GIS, 3D Viewer |
| `manifest.json` | Three.js/CesiumJS compatible | Web 3D |
| `unified.csv` | 22-col unified (1obj=1row) | Knowledge Graph |
| `mesh/{uuid}.glb` | glTF 2.0 Binary | Three.js, Blender |
| `adjacency.csv` | Spatial adjacency | Network Analysis |
| `spatial_relationships.ttl` | RDF triples | SPARQL, Neo4j |

## License

MIT
