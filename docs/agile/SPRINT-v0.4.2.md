# Sprint: DXTnavis v0.4.2 Unit Column & CSV Enhancement

| Field | Value |
|-------|-------|
| **Sprint Name** | DXTnavis Unit Column & CSV Enhancement v0.4.2 |
| **Start Date** | 2026-01-09 |
| **End Date** | 2026-01-09 |
| **Status** | ✅ Completed |
| **Goal** | 중앙 패널에 Unit 컬럼 추가 및 CSV 출력 옵션 확장 |

---

## Requirements Summary

```
Total Features: 4
New Features: 2
Enhancements: 2
```

---

## Phase 1: Unit Column Feature

### 1.1 HierarchicalPropertyRecord 확장
| Field | Value |
|-------|-------|
| Priority | 🔴 Critical |
| Type | Enhancement |
| File | `Models/HierarchicalPropertyRecord.cs` |
| Description | Unit 관련 필드 추가 (DataType, RawValue, NumericValue, Unit) |

**New Fields:**
```csharp
/// <summary>
/// 데이터 타입 (예: "Double", "Int32", "DisplayLength")
/// </summary>
public string DataType { get; set; }

/// <summary>
/// 파싱된 원본 값 (타입 접두사 제거됨)
/// </summary>
public string RawValue { get; set; }

/// <summary>
/// 숫자 값 (파싱 가능한 경우)
/// </summary>
public double? NumericValue { get; set; }

/// <summary>
/// 단위 (예: "m", "mm", "sq m")
/// 단위가 없으면 빈 문자열
/// </summary>
public string Unit { get; set; }
```

**Tasks:**
- [x] DataType 필드 추가
- [x] RawValue 필드 추가
- [x] NumericValue 필드 추가
- [x] Unit 필드 추가
- [x] 생성자 업데이트

### 1.2 NavisworksDataExtractor 수정
| Field | Value |
|-------|-------|
| Priority | 🔴 Critical |
| Type | Enhancement |
| File | `Services/NavisworksDataExtractor.cs` |
| Description | 추출 시점에 DisplayStringParser를 사용하여 파싱 |

**Current:**
```csharp
propertyValue = property.Value.ToString();
```

**Target:**
```csharp
var displayString = property.Value.ToString();
var parsed = _displayStringParser.Parse(displayString);

// 원본 PropertyValue 유지
propertyValue = displayString;

// 파싱된 값 저장
dataType = parsed.DataType;
rawValue = parsed.RawValue;
numericValue = parsed.NumericValue;
unit = parsed.Unit;
```

**Tasks:**
- [x] DisplayStringParser 인스턴스 추가
- [x] TraverseAndExtractProperties 메서드에서 파싱 적용
- [x] 파싱된 값을 HierarchicalPropertyRecord에 저장
- [x] ExtractProperties 메서드에도 파싱 적용

### 1.3 DXwindow.xaml Unit 컬럼 추가
| Field | Value |
|-------|-------|
| Priority | 🟠 High |
| Type | Enhancement |
| File | `Views/DXwindow.xaml` |
| Description | DataGrid에 Unit 컬럼 추가 |

**Current Columns:**
- ✓ (CheckBox)
- Level
- Object
- Category
- Property
- Value

**New Columns:**
- ✓ (CheckBox)
- Level
- Object
- Category
- Property
- Value (원본)
- Unit (파싱된 단위, 없으면 빈 셀)

**Tasks:**
- [x] DataGrid에 Unit 컬럼 추가
- [x] 적절한 컬럼 폭 설정

---

## Phase 2: CSV Export Options

### 2.1 Export Format Selection UI
| Field | Value |
|-------|-------|
| Priority | 🟡 Medium |
| Type | New Feature |
| File | `Views/DXwindow.xaml`, `ViewModels/DXwindowViewModel.cs` |
| Description | CSV 출력 시 Raw/Refined 형식 선택 옵션 |

**Options:**
1. **Raw Only**: 원본 Value만 출력
2. **Refined Only**: DataType, RawValue, NumericValue, Unit 분리 출력
3. **Both**: Raw와 Refined 동시 저장 (현재 구현됨)

**UI Design:**
```
Export Format: [Raw] [Refined] [Both (Default)]
```

**Tasks:**
- [x] HierarchyFileWriter.WriteToCsv에 includeUnit 파라미터 추가
- [x] 기본값으로 Unit 포함 (includeUnit=true)
- [x] JSON Export에도 DataType, Unit 필드 추가

### 2.2 All Properties/Hierarchy 옵션 통합
| Field | Value |
|-------|-------|
| Priority | 🟡 Medium |
| Type | Enhancement |
| File | `Services/PropertyFileWriter.cs` |
| Description | 4종 CSV 출력에 형식 선택 옵션 적용 |

**Tasks:**
- [x] All Hierarchy 출력에 Unit 컬럼 기본 포함
- [x] Selection Hierarchy 출력에 Unit 컬럼 기본 포함
- [x] JSON Export에 DataType, Unit 포함

---

## Technical Design

### Data Flow

```
property.Value.ToString()
        ↓
DisplayStringParser.Parse()
        ↓
ParsedDisplayString {
    DataType: "DisplayLength"
    RawValue: "5.5 m"
    NumericValue: 5.5
    Unit: "m"
    OriginalString: "DisplayLength: 5.5 m"
}
        ↓
HierarchicalPropertyRecord {
    PropertyValue: "DisplayLength: 5.5 m"  // 원본 유지
    DataType: "DisplayLength"
    RawValue: "5.5 m"
    NumericValue: 5.5
    Unit: "m"
}
        ↓
DataGrid Display:
    Value: "DisplayLength: 5.5 m"
    Unit: "m"
```

### Unit Column Display Rules
- 단위가 있는 경우: 단위 표시 (예: "m", "sq m", "deg")
- 단위가 없는 경우: 빈 셀 (empty string)
- 파싱 실패 시: 빈 셀

---

## Success Criteria

- [x] 중앙 패널 DataGrid에 Unit 컬럼 표시
- [x] 단위가 있는 데이터에 단위 분리 표시
- [x] 단위가 없는 데이터는 빈 셀
- [x] CSV Export에 Unit 컬럼 기본 포함
- [x] JSON Export에 DataType, Unit 필드 포함

---

## Notes

### Display Format Examples
| OriginalString | Value Column | Unit Column |
|----------------|--------------|-------------|
| `DisplayLength: 5.5 m` | DisplayLength: 5.5 m | m |
| `Double: 3.14` | Double: 3.14 | |
| `Int32: 42` | Int32: 42 | |
| `DisplayArea: 25.5 sq m` | DisplayArea: 25.5 sq m | sq m |
| `String: some text` | String: some text | |

### Constraints
- 원본 Value는 그대로 유지
- 단위 변환 없음 (원본 단위 그대로 표시)
- 파싱 실패 시 graceful degradation

---

**Created**: 2026-01-09
**Completed**: 2026-01-09
