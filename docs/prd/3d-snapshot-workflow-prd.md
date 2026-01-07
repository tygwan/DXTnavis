# PRD: DXTnavis 3D Snapshot Workflow

## 문서 정보
| 항목 | 내용 |
|------|------|
| 버전 | v1.0 |
| 작성일 | 2025-12-29 |
| 상태 | Draft |
| 담당자 | DXTnavis Team |

---

## 1. 개요

### 1.1 배경
Navisworks 모델에서 속성 데이터를 추출하고, 실제 3D 객체를 식별하여 시각적 스냅샷과 함께 계층화된 메타데이터로 저장하는 통합 워크플로우가 필요함.

### 1.2 목표
- 모델 전체 속성을 CSV로 추출
- 조건부 필터링으로 실제 3D 객체 식별
- 필터링된 객체 리스트 생성 및 관리
- 각 객체의 3D 스냅샷 자동 캡처
- 계층화된 메타데이터 구조로 저장

### 1.3 성공 지표
- 속성 추출 정확도 ≥ 99%
- 3D 객체 식별률 ≥ 95%
- 스냅샷 캡처 성공률 ≥ 98%
- 처리 속도: 1,000 객체 / 5분 이내

---

## 2. 사용자 스토리

### US-01: 속성 전체 추출
> "사용자로서, 모델의 모든 속성을 CSV 파일로 내보내고 싶다"

**수락 조건:**
- [ ] 모든 객체의 PropertyCategories 추출
- [ ] ObjectId, ParentId, Level 계층 정보 포함
- [ ] UTF-8 인코딩으로 한글 지원
- [ ] 진행률 표시 및 취소 기능

### US-02: 3D 객체 필터링
> "사용자로서, 실제 형상이 있는 3D 객체만 필터링하고 싶다"

**수락 조건:**
- [ ] HasGeometry == true 조건 필터링
- [ ] IsHidden == false 조건 필터링
- [ ] 사용자 정의 속성 조건 추가 가능
- [ ] 필터 프리셋 저장/불러오기

### US-03: 조건부 리스트 관리
> "사용자로서, 필터링된 객체 리스트를 관리하고 싶다"

**수락 조건:**
- [ ] 필터 결과를 Selection Set으로 저장
- [ ] 리스트 내보내기 (CSV/JSON)
- [ ] 개별 객체 선택/해제
- [ ] 리스트 갱신 및 동기화

### US-04: 3D 스냅샷 캡처
> "사용자로서, 각 객체의 3D 뷰 스냅샷을 자동으로 캡처하고 싶다"

**수락 조건:**
- [ ] 객체 중심으로 카메라 자동 이동
- [ ] 객체 격리(Isolate) 후 캡처
- [ ] 이미지 형식 선택 (PNG/JPG)
- [ ] 해상도 설정 (720p/1080p/4K)
- [ ] 배치 처리 지원

---

## 3. 기능 요구사항

### 3.1 Phase 1: All Properties CSV 생성

| ID | 요구사항 | 우선순위 |
|----|---------|---------|
| FR-101 | 모델 전체 PropertyCategories 순회 | P0 |
| FR-102 | 계층 구조 정보 (ObjectId, ParentId, Level) 포함 | P0 |
| FR-103 | 진행률 실시간 표시 | P1 |
| FR-104 | 대용량 모델 스트리밍 처리 | P1 |
| FR-105 | 인코딩 자동 감지 및 변환 | P2 |

### 3.2 Phase 2: 조건부 필터링

| ID | 요구사항 | 우선순위 |
|----|---------|---------|
| FR-201 | HasGeometry 필터 | P0 |
| FR-202 | IsHidden 필터 | P0 |
| FR-203 | 속성 기반 조건 필터 (Category, Property, Value) | P0 |
| FR-204 | 복합 조건 (AND/OR) 지원 | P1 |
| FR-205 | 필터 프리셋 저장 | P2 |

### 3.3 Phase 3: 리스트 관리

| ID | 요구사항 | 우선순위 |
|----|---------|---------|
| FR-301 | 필터 결과 DataGrid 표시 | P0 |
| FR-302 | Selection Set 생성 및 저장 | P0 |
| FR-303 | 리스트 CSV/JSON 내보내기 | P1 |
| FR-304 | 개별 항목 선택/해제 체크박스 | P1 |
| FR-305 | 정렬 및 검색 기능 | P2 |

### 3.4 Phase 4: 3D Snapshot 캡처

| ID | 요구사항 | 우선순위 |
|----|---------|---------|
| FR-401 | 객체 중심 카메라 이동 (ZoomToSelection) | P0 |
| FR-402 | 객체 격리 (Isolate) | P0 |
| FR-403 | 화면 캡처 및 이미지 저장 | P0 |
| FR-404 | 배치 처리 (다중 객체 연속 캡처) | P1 |
| FR-405 | 캡처 설정 (해상도, 형식, 배경색) | P1 |
| FR-406 | 캡처 이미지와 메타데이터 연결 | P0 |

---

## 4. 데이터 구조

### 4.1 계층화된 메타데이터 스키마

```
📁 export_output/
├── 📄 all_properties.csv           # Phase 1 출력
├── 📄 filtered_objects.json        # Phase 2-3 출력
├── 📁 snapshots/                   # Phase 4 출력
│   ├── 📷 {ObjectId}_front.png
│   ├── 📷 {ObjectId}_iso.png
│   └── ...
└── 📄 metadata.json                # 통합 메타데이터
```

### 4.2 metadata.json 구조

```json
{
  "exportInfo": {
    "timestamp": "2025-12-29T10:00:00Z",
    "modelPath": "project.nwd",
    "totalObjects": 1500,
    "filteredObjects": 450
  },
  "objects": [
    {
      "objectId": "guid-xxx",
      "parentId": "guid-yyy",
      "level": 2,
      "displayName": "Wall-001",
      "hasGeometry": true,
      "properties": {
        "Category|PropertyName": "Value"
      },
      "snapshots": [
        "snapshots/guid-xxx_front.png",
        "snapshots/guid-xxx_iso.png"
      ]
    }
  ]
}
```

---

## 5. UI/UX 요구사항

### 5.1 워크플로우 탭 추가

기존 DXTnavis UI에 "3D Snapshot Workflow" 탭 추가:

```
┌─────────────────────────────────────────────────────────┐
│  [🔍 검색 세트 생성] [⚙️ 설정] [📸 3D Snapshot Workflow] │
├─────────────────────────────────────────────────────────┤
│  Step 1: CSV 생성  →  Step 2: 필터링  →  Step 3: 캡처   │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐ │
│  │ [CSV 생성]   │   │ [필터 적용]  │   │ [캡처 시작]  │ │
│  │ 진행률: 45%  │   │ 결과: 450개  │   │ 0/450 완료   │ │
│  └──────────────┘   └──────────────┘   └──────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### 5.2 진행 상태 표시

- 각 단계별 진행률 ProgressBar
- 현재 처리 중인 객체 정보 표시
- 취소 버튼 및 일시정지 기능
- 완료 시 결과 요약 표시

---

## 6. 비기능 요구사항

| ID | 요구사항 | 기준 |
|----|---------|------|
| NFR-01 | 성능 | 1,000 객체 처리 < 5분 |
| NFR-02 | 메모리 | 최대 500MB 사용 |
| NFR-03 | 안정성 | 크래시 없이 대용량 처리 |
| NFR-04 | 호환성 | Navisworks 2025 필수 |
| NFR-05 | 스레드 안전성 | UI 스레드 블로킹 방지 |

---

## 7. 제약사항

### 7.1 기술적 제약
- Navisworks API는 UI 스레드에서만 호출 가능
- .NET Framework 4.8 제한으로 일부 최신 C# 기능 사용 불가
- 스냅샷 캡처 시 Navisworks 뷰포트 직접 조작 필요

### 7.2 의존성
- Autodesk.Navisworks.Api.dll
- Autodesk.Navisworks.Automation.dll
- Newtonsoft.Json (JSON 직렬화)
- System.Drawing (이미지 처리)

---

## 8. 마일스톤

| Phase | 기능 | 목표일 | 상태 |
|-------|------|--------|------|
| Phase 1 | All Properties CSV | - | ✅ 완료 |
| Phase 2 | 조건부 필터링 | - | 🔄 개발중 |
| Phase 3 | 리스트 관리 | - | ⏳ 예정 |
| Phase 4 | 3D Snapshot 캡처 | - | ⏳ 예정 |

---

## 9. 참조 문서

- [CLAUDE.md](../../CLAUDE.md) - 프로젝트 개요
- [기술 설계서](../tech-specs/3d-snapshot-workflow-spec.md)
- [진행상황](../progress/3d-snapshot-workflow-progress.md)
