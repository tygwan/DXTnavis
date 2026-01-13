# Phase 5: Data Validation

> **Status:** ✅ Complete (v0.7.0)
> **Parent:** [_INDEX](../_INDEX.md) | **Prev:** [Phase 4](phase-4-snapshot-workflow.md)
> **Last Updated:** 2026-01-13

## Overview
속성 데이터의 유효성 검증 및 리포트 생성

## Requirements
| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-501 | 단위 불일치 감지 | P1 | ✅ CheckUnitConsistency() |
| FR-502 | 필수 속성 누락 확인 | P1 | ✅ CheckRequiredProperties() |
| FR-503 | 데이터 타입 불일치 감지 | P2 | ✅ CheckDataTypeConsistency() |
| FR-504 | 검증 리포트 생성 | P1 | ✅ GenerateReportJson/Summary() |
| FR-505 | 자동 수정 제안 | P2 | ✅ Suggestion 필드 포함 |

## Implementation

### Key Files
- `Services/ValidationService.cs` - 검증 서비스 (~350 lines)
- `ViewModels/DXwindowViewModel.cs` - ValidatePropertiesCommand

### Validation Features

#### Unit Mismatch Detection (FR-501)
- mm vs m vs ft 혼용 감지
- kg vs lbm 혼용 감지
- 동일 카테고리 내 단위 통일성 검사
- Regex 기반 패턴 매칭

#### Missing Property Check (FR-502)
- 필수 속성 정의 (Item/이름, Item/유형)
- 한영 카테고리 모두 지원
- 커스터마이즈 가능한 규칙

#### Data Type Consistency (FR-503)
- Double, Int32, Boolean, DisplayString 타입 검증
- PropertyValue 형식과 DataType 일치 확인

### Validation Report Format
```json
{
  "summary": {
    "validationDate": "2026-01-13 12:00:00",
    "totalObjects": 1500,
    "totalProperties": 45000,
    "validObjects": 1450,
    "warningCount": 45,
    "errorCount": 5
  },
  "issues": [
    {
      "objectId": "guid-xxx",
      "objectName": "Wall-001",
      "type": "UnitMismatch",
      "severity": "Warning",
      "category": "Dimensions",
      "details": "length 단위 혼용: mm, m",
      "suggestion": "단위를 mm로 통일하세요"
    }
  ]
}
```

## UI Integration
- Filter 영역에 "✓ Validate" 버튼 추가
- StatusMessage에 검증 결과 요약 표시
- Debug Output에 상세 리포트 출력

## Dependencies
- Phase 1 (CSV Export) - 데이터 소스
- Phase 2 (Filtering) - 대상 객체 선택
