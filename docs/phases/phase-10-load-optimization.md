# Phase 10: Load Hierarchy Optimization

> **Status:** 📋 Planning
> **Parent:** [_INDEX](../_INDEX.md) | **Prev:** [Phase 9](phase-9-ui-enhancement.md)
> **Target Version:** v0.8.0
> **Last Updated:** 2026-01-13

## Overview
Load Hierarchy 로딩 성능 최적화 - 대용량 모델에서의 응답성 개선

## Problem Analysis

### 현재 구현의 문제점

| Issue | Description | Impact |
|-------|-------------|--------|
| **UI 블로킹** | 동기 실행으로 UI 프리징 | 사용자 경험 저하 |
| **이중 순회** | TreeNodeModel과 HierarchicalPropertyRecord 별도 순회 | 로딩 시간 2배 |
| **진행률 없음** | 로딩 중 피드백 부재 | 사용자 혼란 |
| **취소 불가** | 긴 작업 중단 불가 | 작업 효율성 저하 |
| **메모리 비효율** | 모든 노드 한번에 생성 | 메모리 과사용 |

### 현재 코드 (DXwindowViewModel.cs:984-1068)
```csharp
private Task LoadModelHierarchyAsync()
{
    // 문제 1: Task를 반환하지만 실제로 동기 실행
    // 문제 2: BuildTreeFromModelItem으로 한번, TraverseAndExtractProperties로 또 순회
    foreach (var model in doc.Models)
    {
        var rootNode = BuildTreeFromModelItem(model.RootItem, 0, allNodes);
        // ...
    }

    foreach (var model in doc.Models)
    {
        extractor.TraverseAndExtractProperties(model.RootItem, ...);
    }
}
```

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-1001 | 비동기 로딩 (UI 응답성 유지) | P0 | 📋 Planned |
| FR-1002 | 진행률 표시 (ProgressBar + StatusMessage) | P0 | 📋 Planned |
| FR-1003 | 취소 기능 (CancellationToken) | P1 | 📋 Planned |
| FR-1004 | 단일 순회 (TreeNode + Property 동시 추출) | P1 | 📋 Planned |
| FR-1005 | 가상화/지연 로딩 (VirtualizingStackPanel) | P2 | 📋 Planned |
| FR-1006 | 배치 UI 업데이트 (청크 단위) | P2 | 📋 Planned |

## Technical Design

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    LoadHierarchyService                      │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ ModelTraverser│  │ ProgressReporter│  │ CancellationHandler│  │
│  │  (Single Pass)│  │  (IProgress<T>) │  │  (CancellationToken)│  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘  │
│         │                │                     │              │
│         ▼                ▼                     ▼              │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │              Unified Traversal Engine                   │ │
│  │  - TreeNodeModel 생성                                    │ │
│  │  - HierarchicalPropertyRecord 생성                       │ │
│  │  - 진행률 보고 (노드 카운트 기반)                          │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Implementation Plan

#### Sprint 1: 비동기 로딩 + 진행률 (P0)

```csharp
// 1. 진행률 모델
public class LoadProgress
{
    public int ProcessedNodes { get; set; }
    public int TotalNodes { get; set; }
    public string CurrentItem { get; set; }
    public double Percentage => TotalNodes > 0 ? (double)ProcessedNodes / TotalNodes * 100 : 0;
}

// 2. 비동기 로딩 메서드
private async Task LoadModelHierarchyAsync(
    IProgress<LoadProgress> progress,
    CancellationToken cancellationToken)
{
    // 1단계: 노드 수 카운트 (빠른 순회)
    int totalNodes = await CountNodesAsync(doc.Models, cancellationToken);

    // 2단계: 데이터 추출 (단일 순회)
    var result = await Task.Run(() =>
        ExtractAllInSinglePass(doc.Models, progress, totalNodes, cancellationToken),
        cancellationToken);

    // 3단계: UI 업데이트 (메인 스레드)
    await UpdateUIAsync(result);
}
```

#### Sprint 2: 취소 기능 (P1)

```csharp
// ViewModel에 추가
private CancellationTokenSource _loadCts;
public bool IsLoading { get; private set; }
public ICommand CancelLoadCommand { get; }

private void CancelLoad()
{
    _loadCts?.Cancel();
    StatusMessage = "Loading cancelled";
}
```

#### Sprint 3: 단일 순회 최적화 (P1)

```csharp
// 통합 추출 결과
public class HierarchyExtractionResult
{
    public List<TreeNodeModel> TreeNodes { get; set; }
    public List<HierarchicalPropertyRecord> Properties { get; set; }
    public int TotalNodeCount { get; set; }
}

// 단일 순회 메서드
private HierarchyExtractionResult ExtractAllInSinglePass(
    ModelCollection models,
    IProgress<LoadProgress> progress,
    int totalNodes,
    CancellationToken ct)
{
    var result = new HierarchyExtractionResult();
    int processed = 0;

    foreach (var model in models)
    {
        ct.ThrowIfCancellationRequested();
        TraverseUnified(model.RootItem, 0, result, ref processed, progress, totalNodes, ct);
    }

    return result;
}

private void TraverseUnified(
    ModelItem item, int level, HierarchyExtractionResult result,
    ref int processed, IProgress<LoadProgress> progress, int total, CancellationToken ct)
{
    if (item == null || item.IsHidden) return;
    ct.ThrowIfCancellationRequested();

    // TreeNodeModel 생성
    var treeNode = new TreeNodeModel { ... };
    result.TreeNodes.Add(treeNode);

    // HierarchicalPropertyRecord 생성 (속성 있는 경우만)
    foreach (var category in item.PropertyCategories)
    {
        // ... 속성 추출
        result.Properties.Add(record);
    }

    // 진행률 보고
    processed++;
    if (processed % 100 == 0) // 100개마다 업데이트
    {
        progress?.Report(new LoadProgress
        {
            ProcessedNodes = processed,
            TotalNodes = total,
            CurrentItem = item.DisplayName
        });
    }

    // 자식 재귀
    foreach (var child in item.Children)
    {
        TraverseUnified(child, level + 1, result, ref processed, progress, total, ct);
    }
}
```

#### Sprint 4: 가상화 + 지연 로딩 (P2)

```xml
<!-- DXwindow.xaml - TreeView 가상화 -->
<TreeView ItemsSource="{Binding ObjectHierarchyRoot}"
          VirtualizingStackPanel.IsVirtualizing="True"
          VirtualizingStackPanel.VirtualizationMode="Recycling">
```

### UI 변경사항

```
┌───────────────────────────────────────────────────────────────┐
│  [Load Hierarchy] [Cancel]                                    │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │ ████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ 35% │  │
│  └─────────────────────────────────────────────────────────┘  │
│  Loading: Wall-001 (3,500 / 10,000 nodes)                     │
└───────────────────────────────────────────────────────────────┘
```

## Performance Targets

| Metric | Current | Target | Improvement |
|--------|---------|--------|-------------|
| 10K 노드 로딩 시간 | ~15초 | ~5초 | 3x 향상 |
| UI 응답성 | 0 FPS (프리징) | 60 FPS | 무한대 |
| 메모리 피크 | ~500MB | ~300MB | 40% 감소 |
| 취소 응답 시간 | 불가 | <500ms | - |

## Dependencies

- Phase 1 (CSV Export) - NavisworksDataExtractor 공유
- Phase 2 (Filtering) - FilteredHierarchicalProperties 연동
- .NET 4.8 async/await 패턴

## Files to Modify

| File | Changes | Priority |
|------|---------|----------|
| `Services/LoadHierarchyService.cs` | NEW - 최적화된 로딩 서비스 | P0 |
| `ViewModels/DXwindowViewModel.cs` | LoadModelHierarchyAsync 리팩토링 | P0 |
| `ViewModels/DXwindowViewModel.Tree.cs` | BuildTreeFromModelItem 통합 | P1 |
| `Views/DXwindow.xaml` | ProgressBar, Cancel 버튼 추가 | P0 |
| `Models/LoadProgress.cs` | NEW - 진행률 모델 | P0 |

## Sprint Schedule

| Sprint | Tasks | Target |
|--------|-------|--------|
| Sprint 1 | FR-1001, FR-1002 (비동기 + 진행률) | Week 1 |
| Sprint 2 | FR-1003 (취소 기능) | Week 1 |
| Sprint 3 | FR-1004 (단일 순회) | Week 2 |
| Sprint 4 | FR-1005, FR-1006 (가상화 + 배치) | Week 2 |

## Risk & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Navisworks API 스레드 안전성 | High | UI 스레드에서만 API 호출 |
| 메모리 압박 | Medium | 청크 단위 처리, GC 최적화 |
| 기존 기능 회귀 | Medium | 단위 테스트 작성 |

## Success Criteria

- [ ] 10K 노드 모델 로딩 시 UI 프리징 없음
- [ ] 진행률 표시 정확도 ±5%
- [ ] 취소 시 500ms 내 응답
- [ ] 기존 필터링/검색 기능 정상 동작
- [ ] 메모리 사용량 40% 감소
