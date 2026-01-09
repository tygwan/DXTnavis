# ADR-001: ComAPI를 통한 Custom Property Write 구현

| Field | Value |
|-------|-------|
| **Status** | ✅ Accepted |
| **Date** | 2026-01-09 |
| **Decision Makers** | Development Team |
| **Category** | Architecture |

---

## Context

DXTnavis 플러그인에서 외부 데이터(예: 공정 일정, 비용 정보 등)를 Navisworks 객체의 속성으로 추가하는 기능이 필요합니다.

### 문제점
- .NET API (`Autodesk.Navisworks.Api`)는 **Property 읽기만 지원** (Read-Only)
- 기존 PropertyCategories에 직접 속성 추가 불가
- 외부 데이터와 Navisworks 객체 연동 방법 필요

---

## Research Findings

### 1. .NET API 한계
```csharp
// ❌ 불가능: .NET API로 Property 직접 추가
item.PropertyCategories.Add(newCategory); // 지원하지 않음
```

### 2. ComAPI Solution ✅

ComAPI를 통해 **Custom Property 추가/수정/삭제** 가능:

```csharp
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;

// 1. COM State 획득
InwOpState10 comState = ComApiBridge.State;

// 2. ModelItem → COM Path 변환
InwOaPath comPath = ComApiBridge.ToInwOaPath(modelItem);

// 3. Property Node 획득
InwGUIPropertyNode2 propNode = (InwGUIPropertyNode2)comState.GetGUIPropertyNode(comPath, true);

// 4. Property Vector 생성
InwOaPropertyVec propVec = (InwOaPropertyVec)comState.ObjectFactory(
    nwEObjectType.eObjectType_nwOaPropertyVec, null, null);

// 5. Property 생성 및 추가
InwOaProperty prop = (InwOaProperty)comState.ObjectFactory(
    nwEObjectType.eObjectType_nwOaProperty, null, null);
prop.name = "ScheduleDate_Internal";
prop.UserName = "Schedule Date";
prop.value = "2026-01-15";
propVec.Properties().Add(prop);

// 6. User Data로 설정 (0 = 새로 생성)
propNode.SetUserDefined(0, "DXTnavis Schedule", "DXTnavis_Schedule_Internal", propVec);
```

### 3. SetUserDefined Parameters

```csharp
SetUserDefined(int index, string userName, string internalName, InwOaPropertyVec properties)
```

| Parameter | Description |
|-----------|-------------|
| `index` | `0` = 새로 생성, `1+` = 기존 덮어쓰기 |
| `userName` | UI에 표시될 카테고리 이름 |
| `internalName` | 내부 식별자 |
| `properties` | 속성 벡터 |

### 4. 속성 삭제

```csharp
propNode.RemoveUserDefined(index);
```

---

## Decision

**ComAPI의 `SetUserDefined()` 메서드를 사용하여 Custom Property Write 기능 구현**

### 구현 계획

1. **PropertyWriteService 클래스 생성**
   - `AddCustomProperty(ModelItem item, string category, string name, string value)`
   - `UpdateCustomProperty(ModelItem item, string category, string name, string newValue)`
   - `RemoveCustomProperty(ModelItem item, string category)`

2. **CSV Import 기능과 연동**
   - CSV 파일의 공정 일정 데이터를 객체 속성으로 추가
   - SyncID 기반 객체 매칭

3. **UI 통합**
   - "Import Schedule Data" 버튼 추가
   - 속성 추가 결과 표시

---

## Consequences

### 장점 ✅
- 외부 데이터를 Navisworks 속성으로 통합
- Timeliner와 연동 가능성
- NWD/NWF 파일에 데이터 저장됨

### 단점 ⚠️
- ComAPI 사용으로 코드 복잡성 증가
- Legacy API 의존
- 대량 객체 처리 시 성능 고려 필요

### 위험 🔴
- Navisworks 버전 업그레이드 시 API 변경 가능성
- COM Interop 관련 예외 처리 필요

---

## References

- [TwentyTwo: Navisworks COM API Custom Property](https://twentytwo.space/2020/07/18/navisworks-api-com-interface-and-adding-custom-property/)
- [TwentyTwo: Adding Property to Existing Category](https://twentytwo.space/2020/12/19/navisworks-api-adding-property-to-existing-category/)
- [Autodesk Blog: Add Custom Properties](https://blog.autodesk.io/add-custom-properties-to-all-desired-model-items/)
- [GitHub: Navisworks Property Database Example](https://github.com/xiaodongliang/Navisworks-Net-Plugin-Property-Database-Example)

---

**Created**: 2026-01-09
**Last Updated**: 2026-01-09
