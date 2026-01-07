# DXrevit 플러그인 업데이트 요약 (v2.0)

**작성일**: 2025-10-19
**프로젝트**: AWP 2025 BIM Data Integration System
**목적**: 새 데이터베이스 스키마 v2.0 대응
**상태**: 🔄 코드 완성 (테스트 필요)

---

## 📋 업데이트 개요

DXrevit 플러그인을 새 데이터베이스 스키마 v2.0에 맞게 업데이트했습니다.

### 주요 변경사항
1. ✅ **ProjectManager** 추가 - 프로젝트 자동 등록 및 관리
2. ✅ **RevisionManager** 추가 - 리비전 자동 번호 할당
3. ✅ **DataExtractorV2** 추가 - 새 API 형식 대응
4. ✅ **SnapshotViewModelV2** 추가 - 새 워크플로우 구현

---

## 🆕 새로 추가된 파일

### 1. Services/ProjectManager.cs
**목적**: 프로젝트 자동 등록 및 관리

**주요 기능**:
```csharp
// 1. 파일명에서 프로젝트 코드 자동 생성
string projectCode = GenerateProjectCode("배관테스트.rvt");
// → "배관테스트"

// 2. 프로젝트 등록 또는 조회
var projectInfo = await RegisterOrGetProjectAsync(document);
// → { Code: "배관테스트", Name: "배관테스트", ... }

// 3. 프로젝트 통계 조회
var stats = await GetProjectStatsAsync("배관테스트");
// → { TotalRevisions: 3, TotalObjects: 1200, ... }
```

**API 엔드포인트**:
- `POST /api/v1/projects` - 프로젝트 생성
- `GET /api/v1/projects/{project_code}` - 프로젝트 조회
- `GET /api/v1/projects/{project_code}/stats` - 통계 조회

**자동 생성 규칙**:
```
입력                  → 출력
배관테스트.rvt        → 배관테스트
Snowdon Towers.rvt   → SNOWDON_TOWERS
프로젝트 123.rvt     → 프로젝트_123
```

---

### 2. Services/RevisionManager.cs
**목적**: 리비전 관리 및 객체 업로드

**주요 기능**:
```csharp
// 1. 리비전 생성 (자동 번호 할당)
var revision = await CreateRevisionAsync(
    "배관테스트",           // 프로젝트 코드
    "v1.0",                // 버전 태그
    "Initial design",      // 설명
    document               // Revit Document
);
// → { RevisionNumber: 1, VersionTag: "v1.0", ... }

// 2. 최신 리비전 조회
var latest = await GetLatestRevisionAsync("배관테스트");
// → { RevisionNumber: 3, VersionTag: "v1.2", ... }

// 3. 객체 대량 업로드
bool success = await UploadObjectsToRevisionAsync(
    "배관테스트",
    1,                     // 리비전 번호
    objects                // List<ObjectData>
);
```

**API 엔드포인트**:
- `POST /api/v1/projects/{project_code}/revisions` - 리비전 생성
- `GET /api/v1/projects/{project_code}/revisions/latest/revit` - 최신 리비전
- `POST /api/v1/projects/{project_code}/revisions/{revision_number}/objects/bulk` - 객체 업로드

**파일 해시 계산**:
- SHA256 해시로 파일 무결성 검증
- 중복 업로드 방지

---

### 3. Services/DataExtractorV2.cs
**목적**: Revit 데이터 추출 (v2.0 형식)

**주요 변경사항**:

#### Before (v1.0)
```csharp
var extractedData = new ExtractedData {
    Metadata = new MetadataRecord { ModelVersion = "..." },
    Objects = new List<ObjectRecord>(),
    Relationships = new List<RelationshipRecord>()
};
```

#### After (v2.0)
```csharp
var objects = new List<ObjectData> {
    new ObjectData {
        object_id = element.UniqueId,        // Revit UniqueId
        element_id = (int)element.Id.Value,  // Element ID
        display_name = element.Name,
        category = "Walls",
        family = "Basic Wall",
        type = "Generic - 200mm",
        activity_id = "ACT-001",
        properties = { ... },                 // Dictionary
        bounding_box = { MinX, MinY, ... }   // BoundingBoxData
    }
};
```

**개선 사항**:
- ✅ Revit UniqueId 직접 사용 (해시 생성 불필요)
- ✅ JSONB 속성으로 모든 파라미터 저장
- ✅ Level, Workset 정보 자동 추가
- ✅ Element ID 참조 해석 (RefElement Name 포함)

---

### 4. ViewModels/SnapshotViewModelV2.cs
**목적**: 새 워크플로우 UI 로직

**워크플로우 비교**:

#### Before (v1.0)
```
1. ModelVersion 입력 (수동)
2. 데이터 추출
3. API 전송 (단일 엔드포인트)
```

#### After (v2.0)
```
1. 프로젝트 자동 등록/조회  → ProjectManager
2. 최신 리비전 조회         → RevisionManager
3. 새 리비전 생성           → RevisionManager
4. 데이터 추출              → DataExtractorV2
5. 객체 업로드              → RevisionManager
```

**UI 속성**:
```csharp
// 자동으로 표시되는 정보
string ProjectCode              // "배관테스트" (자동 생성)
string ProjectName              // "배관테스트"
int? CurrentRevisionNumber      // 3 (최신 리비전)
ProjectStats ProjectStats       // { TotalRevisions, TotalObjects, ... }

// 사용자 입력
string VersionTag               // "v1.0" (기본값 제공)
string Description              // "2025-10-19 스냅샷" (기본값)
```

**진행률 표시**:
```
0%   → 프로젝트 정보 로딩
5%   → 리비전 생성
10%  → 데이터 추출 시작
75%  → 데이터 추출 완료
80%  → API 전송 중
100% → 완료
```

---

## 🔄 기존 파일 수정 필요

### 1. Commands/SnapshotCommand.cs

#### 현재 코드
```csharp
var viewModel = new SnapshotViewModel(doc);
var view = new SnapshotView { DataContext = viewModel };
view.ShowDialog();
```

#### 수정 후 (v2.0 사용)
```csharp
var viewModel = new SnapshotViewModelV2(doc);  // ← V2 사용
var view = new SnapshotView { DataContext = viewModel };
view.ShowDialog();
```

---

### 2. Views/SnapshotView.xaml (UI 개선)

#### 추가할 UI 요소

```xml
<StackPanel>
    <!-- ========== 프로젝트 정보 (자동 감지) ========== -->
    <GroupBox Header="프로젝트 정보 (자동 감지)" Margin="0,0,0,10">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="120"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- 프로젝트 코드 -->
            <TextBlock Grid.Row="0" Grid.Column="0" Text="프로젝트 코드:"
                       VerticalAlignment="Center" FontWeight="Bold"/>
            <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding ProjectCode}"
                       VerticalAlignment="Center" Foreground="Blue" FontSize="14"/>

            <!-- 프로젝트 이름 -->
            <TextBlock Grid.Row="1" Grid.Column="0" Text="프로젝트 이름:"
                       VerticalAlignment="Center" Margin="0,5,0,0"/>
            <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding ProjectName}"
                       VerticalAlignment="Center" Margin="0,5,0,0"/>

            <!-- 현재 리비전 -->
            <TextBlock Grid.Row="2" Grid.Column="0" Text="현재 리비전:"
                       VerticalAlignment="Center" Margin="0,5,0,0"/>
            <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding CurrentRevisionDisplay}"
                       VerticalAlignment="Center" Margin="0,5,0,0" Foreground="Green"/>
        </Grid>
    </GroupBox>

    <!-- ========== 새 리비전 정보 (사용자 입력) ========== -->
    <GroupBox Header="새 리비전 정보" Margin="0,0,0,10">
        <StackPanel>
            <!-- 버전 태그 -->
            <TextBlock Text="버전 태그:" FontWeight="Bold" Margin="0,0,0,5"/>
            <ComboBox SelectedItem="{Binding VersionTag}" IsEditable="True"
                      IsEnabled="{Binding CanInput}">
                <ComboBoxItem Content="v1.0"/>
                <ComboBoxItem Content="v1.1"/>
                <ComboBoxItem Content="v2.0"/>
                <ComboBoxItem Content="RC1"/>
                <ComboBoxItem Content="DESIGN"/>
                <ComboBoxItem Content="CONSTRUCTION"/>
            </ComboBox>

            <!-- 변경 설명 -->
            <TextBlock Text="변경 설명:" FontWeight="Bold" Margin="0,10,0,5"/>
            <TextBox Text="{Binding Description}" Height="60" TextWrapping="Wrap"
                     AcceptsReturn="True" VerticalScrollBarVisibility="Auto"
                     IsEnabled="{Binding CanInput}"/>
        </StackPanel>
    </GroupBox>

    <!-- ========== 프로젝트 통계 (선택사항) ========== -->
    <GroupBox Header="프로젝트 통계" Margin="0,0,0,10">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0">
                <TextBlock Text="총 리비전" FontSize="10" Foreground="Gray"/>
                <TextBlock Text="{Binding ProjectStats.TotalRevisions}" FontSize="18" FontWeight="Bold"/>
            </StackPanel>

            <StackPanel Grid.Column="1">
                <TextBlock Text="총 객체" FontSize="10" Foreground="Gray"/>
                <TextBlock Text="{Binding ProjectStats.TotalObjects}" FontSize="18" FontWeight="Bold"/>
            </StackPanel>

            <StackPanel Grid.Column="2">
                <TextBlock Text="카테고리" FontSize="10" Foreground="Gray"/>
                <TextBlock Text="{Binding ProjectStats.TotalCategories}" FontSize="18" FontWeight="Bold"/>
            </StackPanel>
        </Grid>
    </GroupBox>

    <!-- ========== 진행 상태 ========== -->
    <GroupBox Header="진행 상태" Margin="0,0,0,10">
        <StackPanel>
            <TextBlock Text="{Binding StatusMessage}" Margin="0,0,0,5"/>
            <ProgressBar Value="{Binding ProgressValue}" Height="20" Minimum="0" Maximum="100"/>
        </StackPanel>
    </GroupBox>

    <!-- ========== 버튼 ========== -->
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
        <Button Content="새로고침" Command="{Binding RefreshCommand}"
                Width="100" Height="35" Margin="0,0,10,0"/>
        <Button Content="저장" Command="{Binding SaveCommand}"
                Width="100" Height="35" Margin="0,0,10,0"
                Style="{StaticResource PrimaryButton}"/>
        <Button Content="취소" Command="{Binding CancelCommand}"
                Width="100" Height="35"/>
    </StackPanel>
</StackPanel>
```

---

## 📊 워크플로우 비교

### Before (v1.0)
```
사용자 입력
  ↓
  ModelVersion: "프로젝트_이름_20251019_103000"  (수동 입력)
  CreatedBy: "hong.gildong"
  Description: "2025-10-19 스냅샷"
  ↓
DataExtractor.ExtractAll()
  ↓
  - Metadata (1개)
  - Objects (852개)
  - Relationships (N개)
  ↓
ApiDataWriter.SendDataAsync()
  ↓
  POST /api/v1/ingest → 단일 엔드포인트
  ↓
완료
```

### After (v2.0)
```
자동 초기화
  ↓
ProjectManager.RegisterOrGetProject(document)
  ↓
  파일명: "배관테스트.rvt"
  → 프로젝트 코드: "배관테스트" (자동 생성)
  ↓
  POST /api/v1/projects (프로젝트 없으면 생성)
  ↓
RevisionManager.GetLatestRevision("배관테스트")
  ↓
  GET /api/v1/projects/배관테스트/revisions/latest/revit
  → Current Revision: #3
  ↓
사용자 입력
  ↓
  VersionTag: "v1.0" (기본값 제공)
  Description: "2025-10-19 스냅샷" (기본값)
  ↓
RevisionManager.CreateRevision()
  ↓
  POST /api/v1/projects/배관테스트/revisions
  → Revision #4 생성
  ↓
DataExtractorV2.ExtractAllObjects()
  ↓
  - Objects (852개, ObjectData 형식)
  ↓
RevisionManager.UploadObjects()
  ↓
  POST /api/v1/projects/배관테스트/revisions/4/objects/bulk
  ↓
통계 갱신
  ↓
  GET /api/v1/projects/배관테스트/stats
  ↓
완료
```

---

## 🧪 테스트 시나리오

### 1. 첫 번째 스냅샷 (신규 프로젝트)
```
입력:
  - Revit 파일: 배관테스트.rvt
  - 버전 태그: v1.0
  - 설명: "Initial design phase"

예상 결과:
  ✅ 프로젝트 "배관테스트" 자동 생성
  ✅ Revision #1 생성
  ✅ 852개 객체 업로드 완료
  ✅ UI에 통계 표시
```

### 2. 두 번째 스냅샷 (기존 프로젝트)
```
입력:
  - 같은 Revit 파일
  - 버전 태그: v1.1
  - 설명: "Design updates"

예상 결과:
  ✅ 기존 프로젝트 "배관테스트" 감지
  ✅ Current Revision: #1 표시
  ✅ Revision #2 생성
  ✅ 객체 업로드 완료
```

### 3. 에러 처리
```
시나리오:
  - API 서버 다운

예상 결과:
  ❌ 프로젝트 등록 실패
  → 사용자에게 오류 메시지 표시
  → 로그 파일에 오류 기록
```

---

## 🔧 통합 방법

### Option 1: ViewModel 교체 (권장)
기존 SnapshotViewModel을 SnapshotViewModelV2로 교체

**장점**: 깔끔한 코드, v2.0 기능 완전 활용
**단점**: UI 수정 필요

### Option 2: 병렬 사용
v1.0과 v2.0을 별도 메뉴로 제공

**장점**: 안전한 마이그레이션, 롤백 가능
**단점**: 코드 중복

---

## 📝 다음 단계

1. **SnapshotCommand.cs 수정**
   - SnapshotViewModelV2 사용하도록 변경

2. **SnapshotView.xaml 업데이트**
   - 프로젝트 정보 섹션 추가
   - 통계 표시 섹션 추가

3. **테스트**
   - 신규 프로젝트 테스트
   - 기존 프로젝트 테스트
   - 에러 시나리오 테스트

4. **배포**
   - DXrevit.dll 빌드
   - Revit 2025 Addins 폴더에 배포
   - 사용자 가이드 업데이트

---

## 📂 생성된 파일 목록

```
DXrevit/
├── Services/
│   ├── ProjectManager.cs          ← 새로 추가 ✅
│   ├── RevisionManager.cs         ← 새로 추가 ✅
│   ├── DataExtractorV2.cs         ← 새로 추가 ✅
│   ├── DataExtractor.cs           (기존 유지)
│   └── ApiDataWriter.cs           (기존 유지)
├── ViewModels/
│   ├── SnapshotViewModelV2.cs     ← 새로 추가 ✅
│   ├── SnapshotViewModel.cs       (기존 유지)
│   └── SettingsViewModel.cs       (기존 유지)
├── Commands/
│   ├── SnapshotCommand.cs         (수정 필요 🔄)
│   └── SettingsCommand.cs         (기존 유지)
└── Views/
    ├── SnapshotView.xaml          (수정 권장 🔄)
    └── SettingsView.xaml          (기존 유지)
```

---

## 결론

DXrevit 플러그인의 핵심 v2.0 코드가 완성되었습니다! 🎉

### 완료된 작업
- ✅ ProjectManager (프로젝트 자동 관리)
- ✅ RevisionManager (리비전 자동 관리)
- ✅ DataExtractorV2 (새 API 형식)
- ✅ SnapshotViewModelV2 (새 워크플로우)

### 남은 작업
- 🔄 SnapshotCommand.cs 수정 (간단)
- 🔄 SnapshotView.xaml UI 개선 (선택)
- 🧪 실제 Revit 환경에서 테스트

**문서 작성자**: System Integration Team
**최종 수정**: 2025-10-19
