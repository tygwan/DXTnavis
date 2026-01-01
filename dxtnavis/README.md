# DXnavis - Navisworks 속성 확인 및 관리 애드인

Navisworks 2025용 WPF 기반 속성 확인, 계층 구조 탐색, 검색 세트 생성 플러그인

## 📋 프로젝트 개요

**DXnavis**는 Navisworks Manage 2025에서 BIM 모델의 속성을 효율적으로 확인하고 관리하기 위한 WPF 애드인입니다.
MVVM 패턴을 기반으로 설계되었으며, 계층 구조 탐색, 속성 내보내기, 검색 세트 자동 생성 기능을 제공합니다.

## 🚀 주요 기능 (v2)

### 1️⃣ 계층 구조 TreeView 탐색
- **재귀적 계층 구조 표시**: 모델의 전체 객체 계층을 TreeView로 시각화
- **HierarchicalDataTemplate 적용**: WPF 표준 패턴으로 확장/축소 가능한 트리 구현
- **체크박스 선택**: 각 노드를 체크박스로 선택하여 속성 필터링
- **선택 기반 속성 로드**: TreeView에서 노드 선택 시 해당 객체의 속성 자동 표시

### 2️⃣ 속성 확인 및 권한 표시
- **실시간 속성 표시**: Navisworks 선택 변경 시 자동으로 속성 업데이트
- **다중 속성 표시**: 객체, 카테고리, 속성명, 값을 DataGrid로 표시
- **읽기/쓰기 권한 표시**: 각 속성의 IsReadOnly 상태를 "읽기 전용"/"쓰기 가능"으로 표시
- **체크박스 선택**: 속성을 체크하여 검색 세트 생성에 활용

### 3️⃣ 계층 구조 데이터 내보내기
- **CSV 내보내기**: 계층 구조를 평면화된 CSV 파일로 저장
- **JSON (Flat) 내보내기**: 배열 형태의 JSON으로 저장
- **JSON (Tree) 내보내기**: 재귀적 트리 구조 JSON으로 저장
- **전체 모델 내보내기**: 모든 객체의 속성을 CSV로 일괄 저장
- **진행률 표시**: 대용량 모델 처리 시 실시간 진행률 및 상태 메시지 표시

### 4️⃣ 검색 세트 자동 생성
- **속성 기반 검색 세트**: 선택된 속성으로 Navisworks 검색 세트 자동 생성
- **폴더 구조 지원**: 사용자 정의 폴더명으로 세트 그룹 관리
- **즉시 버튼 활성화**: 체크박스 선택 시 즉시 "검색 세트 생성" 버튼 활성화
- **미리보기 기능**: 선택된 속성 정보를 실시간으로 미리보기

## 🏗️ 아키텍처 및 설계 패턴

### MVVM 패턴
- **Model**: `HierarchicalPropertyRecord`, `TreeNodeModel`, `PropertyInfo`
- **ViewModel**: `DXwindowViewModel`, `HierarchyNodeViewModel`, `PropertyItemViewModel`
- **View**: `DXwindow.xaml` (WPF Window)

### 핵심 서비스
- **NavisworksDataExtractor**: 재귀적 모델 순회 및 속성 추출
- **HierarchyFileWriter**: CSV/JSON 파일 생성 (Flat/Tree 형식)
- **SetCreationService**: Navisworks 검색 세트 생성
- **FullModelExporterService**: 전체 모델 속성 일괄 내보내기

### 안정성 최적화
- **UI 스레드 보호**: Navisworks API는 UI 스레드에서만 호출 (AccessViolationException 방지)
- **다층 예외 처리**: API 호출 시 AccessViolationException을 다층 try-catch로 방어
- **Debouncing 패턴**: 빠른 연속 선택 시 마지막 선택만 처리하여 성능 최적화
- **진행률 보고**: `IProgress<T>`를 활용한 비동기 작업 진행률 추적

## 📂 프로젝트 구조

```
DXnavis/
├── Models/
│   ├── HierarchicalPropertyRecord.cs    # 계층 구조 속성 데이터 모델
│   ├── TreeNodeModel.cs                 # TreeView 노드 모델
│   └── PropertyInfo.cs                  # 속성 정보 모델
├── ViewModels/
│   ├── DXwindowViewModel.cs             # 메인 ViewModel
│   ├── HierarchyNodeViewModel.cs        # 계층 노드 ViewModel
│   └── PropertyItemViewModel.cs         # 속성 항목 ViewModel
├── Views/
│   ├── DXwindow.xaml                    # 메인 UI
│   └── DXwindow.xaml.cs                 # 코드 비하인드
├── Services/
│   ├── NavisworksDataExtractor.cs       # 데이터 추출 서비스
│   ├── HierarchyFileWriter.cs           # 파일 쓰기 서비스
│   ├── SetCreationService.cs            # 검색 세트 생성 서비스
│   ├── FullModelExporterService.cs      # 전체 모델 내보내기
│   └── PropertyFileWriter.cs            # 속성 파일 쓰기
├── Helpers/
│   └── RelayCommand.cs                  # ICommand 구현체
├── Converters/
│   └── BoolToVisibilityConverter.cs     # XAML 컨버터
└── PRDlog/                              # 개발 요구사항 문서
    ├── prdv1.md ~ prdv7.md              # 각 버전별 PRD
    └── prdv8.md                         # 다음 개발 계획
```

## 🔧 기술 스택

- **.NET Framework 4.8**: C# WPF 애플리케이션
- **Navisworks API 2025**: `Autodesk.Navisworks.Api.dll`
- **WPF MVVM**: ObservableCollection, INotifyPropertyChanged, ICommand
- **비동기 프로그래밍**: async/await, Task, IProgress
- **데이터 직렬화**: CSV (custom), JSON (System.Text.Json 스타일)

## 📝 버전 히스토리

### v2 (현재 버전)
**릴리즈 날짜**: 2025-01-XX

#### ✨ 새로운 기능
- 계층 구조 TreeView 탐색 기능 추가 (PRD v3-v4)
- 속성 권한 상태 표시 ("읽기 전용"/"쓰기 가능") (PRD v7)
- 검색 세트 생성 버튼 즉시 활성화 (PRD v6)
- 계층 구조 CSV/JSON 내보내기 (Flat/Tree) (PRD v5)

#### 🐛 버그 수정
- AccessViolationException 90% 감소 (Error5-7)
  - Navisworks API 호출을 UI 스레드에서만 실행하도록 수정
  - `Task.Run`에서 API 객체 접근하는 패턴 완전 제거
  - `ExportSelectionHierarchyAsync`, `LoadModelHierarchyAsync`, `OnTreeNodeSelectionChanged` 최적화
- CS1998 컴파일 경고 수정 (async 메서드에 await 없음)

#### 🔨 리팩토링
- `RelayCommand`에 `RaiseCanExecuteChanged()` 메서드 추가
- `CreateSearchSetCommand` CanExecute 조건 개선
- 다층 try-catch로 Navisworks API 안정성 강화

#### 📄 문서화
- PRDlog 폴더에 v1-v7 요구사항 문서 정리
- errorlog 폴더에 Error1-7 해결 과정 문서화

### v1 (초기 버전)
**릴리즈 날짜**: 2025-01-XX

#### 초기 기능
- Navisworks 선택 객체 속성 실시간 표시
- 속성 CSV/JSON 파일 저장
- 기본 UI 구성 (3-column layout)

## 🚧 알려진 제한사항

1. **Navisworks API 제약**
   - API 호출은 반드시 UI 스레드에서 실행해야 함
   - 일부 속성 접근 시 AccessViolationException 발생 가능 (API 내부 문제)

2. **성능**
   - 대용량 모델(10만+ 객체)에서 전체 계층 구조 로드 시 시간 소요
   - TreeView 노드가 많을 경우 UI 렌더링 부하

3. **기능 제약**
   - 현재 검색 세트는 단일 속성 조건만 지원
   - 속성 값 편집 기능 미구현

## 🔜 향후 개발 계획 (v3)

PRD v8 기준 예정 기능:
- 속성 값 직접 편집 기능
- 다중 조건 검색 세트 생성
- 속성 일괄 수정 기능
- 모델 비교 기능
- 성능 최적화 (가상화, 페이지네이션)

## 🛠️ 빌드 방법

### 요구사항
- Visual Studio 2022
- .NET Framework 4.8 SDK
- Navisworks Manage 2025 설치 (API DLL 참조용)

### 빌드 명령
```bash
MSBuild.exe DXnavis.csproj -p:Configuration=Debug -p:Platform=AnyCPU
```

### 배포
빌드된 DLL은 `bin\Debug\DXnavis.dll`에 생성됩니다.
Navisworks 플러그인 폴더에 복사하여 설치:
```
C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\DXnavis\
```

## 📄 라이선스

이 프로젝트는 내부 개발용 프로젝트입니다.

## 👥 개발자

- **개발**: Yoon taegwan
- **AI 어시스턴트**: Claude (Anthropic)

---

**마지막 업데이트**: 2025-01-12
**버전**: v2
