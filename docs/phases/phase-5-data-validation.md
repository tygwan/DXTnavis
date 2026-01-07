# Phase 5: Data Validation

> **Status:** 📋 Planned
> **Parent:** [_INDEX](../_INDEX.md) | **Prev:** [Phase 4](phase-4-snapshot-workflow.md)

## Overview
속성 데이터의 유효성 검증 및 리포트 생성

## Planned Requirements
| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-501 | 단위 불일치 감지 | P1 | ⏳ |
| FR-502 | 필수 속성 누락 확인 | P1 | ⏳ |
| FR-503 | 데이터 타입 불일치 감지 | P2 | ⏳ |
| FR-504 | 검증 리포트 생성 | P1 | ⏳ |
| FR-505 | 자동 수정 제안 | P2 | ⏳ |

## Planned Features

### Unit Mismatch Detection
- mm vs m vs ft 혼용 감지
- 동일 카테고리 내 단위 통일성 검사

### Missing Property Check
- 필수 속성 정의 (config)
- 누락된 객체 리스트 생성

### Validation Report
```json
{
  "summary": {
    "totalObjects": 1500,
    "validObjects": 1450,
    "warningCount": 45,
    "errorCount": 5
  },
  "issues": [
    {
      "objectId": "guid-xxx",
      "type": "unit_mismatch",
      "severity": "warning",
      "details": "Length: 1000mm vs 1m"
    }
  ]
}
```

## Implementation Plan
1. ValidationService.cs 생성
2. 검증 규칙 설정 UI 추가
3. 리포트 뷰어 구현

## Dependencies
- Phase 1 (CSV Export) - 데이터 소스
- Phase 2 (Filtering) - 대상 객체 선택
