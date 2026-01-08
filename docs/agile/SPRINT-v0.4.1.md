# Sprint: DXTnavis v0.4.1 Tree Hierarchy Fix

| Field | Value |
|-------|-------|
| **Sprint Name** | DXTnavis Tree Hierarchy Fix v0.4.1 |
| **Start Date** | 2026-01-08 |
| **End Date** | - |
| **Status** | ✅ Completed |
| **Goal** | Navisworks와 동일한 계층 구조 트리뷰 구현 |

---

## Problem Analysis

### Current State (v0.4.0)
```
┌─────────────────────────────────────────────────┐
│ Plugin (좌측)          │ Navisworks (우측)        │
│ ─────────────────────  │ ────────────────────    │
│ ✅ Level 배지 (L0~L8)   │ ❌ Level 표시 없음        │
│ ❌ 평면 리스트          │ ✅ 계층 트리 구조         │
│ ❌ 노드별 Expand/Collapse │ ✅ 각 노드 Expand/Collapse │
│ ❌ 들여쓰기 없음        │ ✅ 계층별 들여쓰기        │
└─────────────────────────────────────────────────┘
```

### Root Cause
1. **HierarchicalPropertyRecord**는 **속성이 있는 객체만** 포함
2. 속성이 없는 **컨테이너 노드**(L1, L3, L6, L7 등)가 트리에서 누락
3. 자식 노드의 ParentId가 누락된 부모를 참조 → 계층 구조 깨짐
4. 부모를 찾지 못하면 노드가 어디에도 추가되지 않음

### Evidence from Screenshot
```
L0: For Review.nwd
L2: Assy_FR_UC_CS_1-1-2  ← L1이 누락되어 바로 L2
L2: HgrAisc31_C3x6-1-C4
L4: MemberPartPrismatic... ← L3이 누락
L5: MemberPartPrismatic...
L8: Footing-Cable...       ← L6, L7이 누락
```

---

## Target State (v0.4.1)

### Goal
Navisworks 선택 트리와 동일한 계층 구조:
- 모든 레벨의 노드 포함 (컨테이너 포함)
- 각 노드별 Expand/Collapse (▶/▼)
- 계층별 들여쓰기
- Level 배지 유지 (플러그인만의 장점)

### Visual Target
```
▼ L0 📁 For Review.nwd (5)
  ▼ L1 📁 Assy_FR_UC_CS_1-1-2 (3)
    ▶ L2 📁 HgrAisc31_C3x6-1-C4 (2)
    ▶ L2 📁 Utility_FOUR_HOLE... (1)
  ▼ L1 📁 TRAINING (8)
    ▼ L2 📁 Area01 (4)
      ▶ L3 🔷 MemberPart-0241
      ▶ L3 🔷 MemberPart-0242
```

---

## Phase 1: Tree Building Fix

### 1.1 LoadHierarchy 메서드 수정
| Field | Value |
|-------|-------|
| Priority | 🔴 Critical |
| Type | Bug Fix |
| File | `DXwindowViewModel.cs` |
| Description | ModelItem 계층 구조를 직접 사용하여 트리 구축 |

**Current Approach (문제):**
```csharp
// HierarchicalPropertyRecord에서 트리 구축
// → 속성 없는 컨테이너 노드 누락
foreach (var record in allData.GroupBy(r => r.ObjectId))
{
    // 부모 찾기 실패 시 노드 누락
    if (nodeMap.TryGetValue(firstRecord.ParentId, out var parentNode))
        parentNode.Children.Add(node);
    // else: 노드 손실!
}
```

**New Approach (해결):**
```csharp
// ModelItem.Children을 직접 사용하여 재귀 트리 구축
private TreeNodeModel BuildTreeFromModelItem(ModelItem item, int level)
{
    var node = new TreeNodeModel
    {
        ObjectId = item.InstanceGuid,
        DisplayName = GetDisplayName(item),
        Level = level,
        HasGeometry = item.HasGeometry
    };

    // 모든 자식 포함 (속성 유무 무관)
    foreach (var child in item.Children)
    {
        var childNode = BuildTreeFromModelItem(child, level + 1);
        if (childNode != null)
            node.Children.Add(childNode);
    }

    return node;
}
```

**Tasks:**
- [x] `BuildTreeFromModelItem()` 재귀 메서드 추가
- [x] `LoadHierarchy`에서 새 메서드 사용
- [x] 컨테이너 노드 포함 확인

### 1.2 누락된 부모 노드 처리
| Field | Value |
|-------|-------|
| Priority | 🔴 Critical |
| Type | Bug Fix |
| File | `DXwindowViewModel.cs` |
| Description | 부모 노드 없을 시 루트에 추가 |

**Fallback Logic:**
```csharp
if (firstRecord.ParentId == Guid.Empty)
{
    ObjectHierarchyRoot.Add(node);
}
else if (nodeMap.TryGetValue(firstRecord.ParentId, out var parentNode))
{
    parentNode.Children.Add(node);
}
else
{
    // ✅ Fallback: 부모 못 찾으면 루트에 추가
    ObjectHierarchyRoot.Add(node);
}
```

**Tasks:**
- [x] else 절 추가하여 orphan 노드 처리 (BuildTreeFromModelItem에서 자동 처리)
- [x] 디버그 로깅 추가 (상태 메시지에 컨테이너 노드 수 표시)

---

## Phase 2: UI Enhancement

### 2.1 TreeView 들여쓰기 개선
| Field | Value |
|-------|-------|
| Priority | 🟠 High |
| Type | Enhancement |
| File | `DXwindow.xaml` |
| Description | Level 기반 들여쓰기로 계층 시각화 |

**Tasks:**
- [x] TreeViewItem에 Level 기반 Margin 추가 (HierarchicalDataTemplate 기본 제공)
- [x] Expander 아이콘 스타일 개선 (WPF TreeView 기본 제공)

### 2.2 노드 아이콘 개선
| Field | Value |
|-------|-------|
| Priority | 🟡 Medium |
| Type | Enhancement |
| File | `TreeNodeModel.cs` |
| Description | 자식 유무에 따른 아이콘 동적 변경 |

**Tasks:**
- [x] HasChildren 속성 추가 (Children.Count > 0으로 NodeIcon에서 처리)
- [x] NodeIcon 동적 업데이트 (기존 구현 유지)

---

## Success Criteria

- [x] 모든 계층 레벨 노드가 트리에 표시됨
- [x] 컨테이너 노드(속성 없음)도 표시됨
- [x] Navisworks 트리와 동일한 부모-자식 관계
- [x] 각 노드별 Expand/Collapse 작동
- [x] Level 배지 유지

---

## Technical Notes

### ModelItem Hierarchy
- `ModelItem.Children`: 직접 자식 컬렉션
- `ModelItem.Parent`: 부모 참조
- `ModelItem.IsHidden`: 숨김 여부
- `ModelItem.HasGeometry`: 형상 유무

### WPF TreeView
- `HierarchicalDataTemplate.ItemsSource`: 자식 바인딩
- `TreeViewItem.IsExpanded`: 확장 상태
- Built-in expander (▶/▼) 자동 표시

---

**Created**: 2026-01-08
**Last Updated**: 2026-01-08
