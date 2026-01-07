using Autodesk.Revit.DB;
using DXBase.Services;
using DXrevit.Services;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DXrevit.ViewModels
{
    /// <summary>
    /// 스냅샷 뷰 모델 v2.0 - 새 스키마 대응
    /// </summary>
    public class SnapshotViewModelV2 : INotifyPropertyChanged
    {
        private readonly Document _document;
        private readonly ProjectManager _projectManager;
        private readonly RevisionManager _revisionManager;
        private readonly DataExtractorV2 _dataExtractor;

        // UI Properties
        private string _projectCode = string.Empty;
        private string _projectName = string.Empty;
        private int? _currentRevisionNumber;
        private string _versionTag = string.Empty;
        private string _description = string.Empty;
        private bool _isProcessing;
        private string _statusMessage = string.Empty;
        private int _progressValue;
        private ProjectStats _projectStats;

        public SnapshotViewModelV2(Document document)
        {
            _document = document;
            _projectManager = new ProjectManager();
            _revisionManager = new RevisionManager();
            _dataExtractor = new DataExtractorV2(document);

            // 진행률 보고자 설정
            var progressReporter = new Utils.ProgressReporter((percentage, message) =>
            {
                ProgressValue = percentage;
                StatusMessage = message;
            });
            _dataExtractor.SetProgressReporter(progressReporter);

            // Dispatcher 초기화
            Utils.DispatcherHelper.Initialize(System.Windows.Application.Current.Dispatcher);

            // 커맨드 초기화
            SaveCommand = new RelayCommand(async () => await ExecuteSaveAsync(), CanExecuteSave);
            RefreshCommand = new RelayCommand(async () => await LoadProjectInfoAsync());
            CancelCommand = new RelayCommand(ExecuteCancel);

            // 초기화
            _ = InitializeAsync();
        }

        #region Properties

        public string ProjectCode
        {
            get => _projectCode;
            set { _projectCode = value; OnPropertyChanged(); }
        }

        public string ProjectName
        {
            get => _projectName;
            set { _projectName = value; OnPropertyChanged(); }
        }

        public int? CurrentRevisionNumber
        {
            get => _currentRevisionNumber;
            set { _currentRevisionNumber = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentRevisionDisplay)); }
        }

        public string CurrentRevisionDisplay =>
            CurrentRevisionNumber.HasValue ? $"Revision #{CurrentRevisionNumber}" : "첫 리비전";

        public string VersionTag
        {
            get => _versionTag;
            set { _versionTag = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set { _isProcessing = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanInput)); }
        }

        public bool CanInput => !IsProcessing;

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        public ProjectStats ProjectStats
        {
            get => _projectStats;
            set { _projectStats = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CancelCommand { get; }

        private bool CanExecuteSave()
        {
            return !IsProcessing &&
                   !string.IsNullOrWhiteSpace(ProjectCode) &&
                   !string.IsNullOrWhiteSpace(VersionTag);
        }

        #endregion

        #region Initialization

        private async Task InitializeAsync()
        {
            try
            {
                StatusMessage = "프로젝트 정보 로딩 중...";
                ProgressValue = 0;

                // 프로젝트 정보 로드
                await LoadProjectInfoAsync();

                StatusMessage = "저장 준비 완료";
                ProgressValue = 0;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("초기화 중 오류", "DXrevit", ex);
                StatusMessage = "초기화 오류 발생";
            }
        }

        private async Task LoadProjectInfoAsync()
        {
            try
            {
                // 1. 프로젝트 등록 또는 조회
                var projectInfo = await _projectManager.RegisterOrGetProjectAsync(_document);

                ProjectCode = projectInfo.Code;
                ProjectName = projectInfo.Name;

                // 2. 최신 리비전 조회
                var latestRevision = await _revisionManager.GetLatestRevisionAsync(ProjectCode);
                CurrentRevisionNumber = latestRevision?.RevisionNumber;

                // 3. 프로젝트 통계 조회
                ProjectStats = await _projectManager.GetProjectStatsAsync(ProjectCode);

                // 4. 기본값 설정
                if (string.IsNullOrEmpty(VersionTag))
                {
                    VersionTag = latestRevision != null
                        ? $"v{latestRevision.RevisionNumber + 1}.0"
                        : "v1.0";
                }

                if (string.IsNullOrEmpty(Description))
                {
                    Description = $"{DateTime.Now:yyyy-MM-dd} 스냅샷";
                }

                LoggingService.LogInfo($"프로젝트 정보 로드 완료: {ProjectCode}", "DXrevit");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("프로젝트 정보 로드 실패", "DXrevit", ex);
                MessageBox.Show(
                    $"프로젝트 정보를 로드할 수 없습니다:\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Save Workflow

        private async Task ExecuteSaveAsync()
        {
            try
            {
                IsProcessing = true;
                ProgressValue = 0;

                // 1. 리비전 생성
                StatusMessage = "리비전 생성 중...";
                ProgressValue = 5;

                var revision = await _revisionManager.CreateRevisionAsync(
                    ProjectCode,
                    VersionTag,
                    Description,
                    _document);

                LoggingService.LogInfo(
                    $"리비전 생성 완료: #{revision.RevisionNumber}",
                    "DXrevit");

                ProgressValue = 10;

                // 2. 데이터 추출 (메인 스레드에서 동기 실행 - Revit API 제약)
                StatusMessage = "Revit 데이터 추출 중...";
                var objects = _dataExtractor.ExtractAllObjects();

                ProgressValue = 75;

                // 3. 객체 데이터 업로드
                StatusMessage = "API 서버로 데이터 전송 중...";
                ProgressValue = 80;

                bool uploadSuccess = await _revisionManager.UploadObjectsToRevisionAsync(
                    ProjectCode,
                    revision.RevisionNumber,
                    objects);

                ProgressValue = 100;

                if (uploadSuccess)
                {
                    StatusMessage = "스냅샷 저장 완료!";

                    // 프로젝트 통계 갱신
                    ProjectStats = await _projectManager.GetProjectStatsAsync(ProjectCode);
                    CurrentRevisionNumber = revision.RevisionNumber;

                    MessageBox.Show(
                        $"스냅샷이 성공적으로 저장되었습니다.\n\n" +
                        $"프로젝트: {ProjectName}\n" +
                        $"리비전: #{revision.RevisionNumber} ({VersionTag})\n" +
                        $"객체 수: {objects.Count}",
                        "저장 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    CloseWindow(true);
                }
                else
                {
                    StatusMessage = "데이터 전송 실패";
                    MessageBox.Show(
                        "데이터 전송에 실패했습니다.\n로그 파일을 확인해주세요.",
                        "저장 실패",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "오류 발생";
                LoggingService.LogError("스냅샷 저장 중 오류", "DXrevit", ex);
                MessageBox.Show(
                    $"오류가 발생했습니다:\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void ExecuteCancel()
        {
            CloseWindow(false);
        }

        private void CloseWindow(bool dialogResult)
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.DialogResult = dialogResult;
                    window.Close();
                    break;
                }
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
