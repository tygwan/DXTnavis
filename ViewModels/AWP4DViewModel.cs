using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DXTnavis.Helpers;
using DXTnavis.Models;
using DXTnavis.Services;
using Microsoft.Win32;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// AWP 4D Automation ViewModel
    /// Phase 8: CSV → Property → Selection Set → TimeLiner Task 자동화
    /// </summary>
    public class AWP4DViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        private readonly AWP4DAutomationService _automationService;
        private readonly AWP4DValidator _validator;
        private CancellationTokenSource _cancellationTokenSource;

        private string _csvFilePath;
        private bool _isRunning;
        private bool _isDryRun;
        private int _progressPercentage;
        private string _currentPhase;
        private string _statusMessage;
        private AutomationResult _lastResult;

        // Options
        private bool _enablePropertyWrite = true;
        private bool _enableSelectionSetCreation = true;
        private bool _enableTimeLinerTaskCreation = true;
        private GroupingStrategy _selectedGroupingStrategy = GroupingStrategy.ByParentSet;
        private double _minMatchSuccessRate = 80.0;
        private string _selectionSetRootFolder = "AWP_4D";
        private string _timeLinerRootFolder = "AWP_4D";

        #endregion

        #region Properties

        /// <summary>
        /// CSV 파일 경로
        /// </summary>
        public string CsvFilePath
        {
            get => _csvFilePath;
            set
            {
                _csvFilePath = value;
                OnPropertyChanged(nameof(CsvFilePath));
                OnPropertyChanged(nameof(CsvFileName));
                OnPropertyChanged(nameof(HasCsvFile));
                ((RelayCommand)ExecuteCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ValidateCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// CSV 파일 이름
        /// </summary>
        public string CsvFileName => string.IsNullOrEmpty(CsvFilePath)
            ? "(No file selected)"
            : Path.GetFileName(CsvFilePath);

        /// <summary>
        /// CSV 파일 선택 여부
        /// </summary>
        public bool HasCsvFile => !string.IsNullOrEmpty(CsvFilePath) && File.Exists(CsvFilePath);

        /// <summary>
        /// 실행 중 여부
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(CanExecute));
                ((RelayCommand)ExecuteCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 드라이런 모드
        /// </summary>
        public bool IsDryRun
        {
            get => _isDryRun;
            set
            {
                _isDryRun = value;
                OnPropertyChanged(nameof(IsDryRun));
            }
        }

        /// <summary>
        /// 실행 가능 여부
        /// </summary>
        public bool CanExecute => HasCsvFile && !IsRunning;

        /// <summary>
        /// 진행률 (0-100)
        /// </summary>
        public int ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                _progressPercentage = value;
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        /// <summary>
        /// 현재 단계
        /// </summary>
        public string CurrentPhase
        {
            get => _currentPhase;
            set
            {
                _currentPhase = value;
                OnPropertyChanged(nameof(CurrentPhase));
            }
        }

        /// <summary>
        /// 상태 메시지
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        /// <summary>
        /// 마지막 실행 결과
        /// </summary>
        public AutomationResult LastResult
        {
            get => _lastResult;
            set
            {
                _lastResult = value;
                OnPropertyChanged(nameof(LastResult));
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(ResultSummary));
            }
        }

        /// <summary>
        /// 결과 존재 여부
        /// </summary>
        public bool HasResult => LastResult != null;

        /// <summary>
        /// 결과 요약
        /// </summary>
        public string ResultSummary => LastResult?.GenerateSummary() ?? string.Empty;

        /// <summary>
        /// 로그 항목 목록
        /// </summary>
        public ObservableCollection<LogEntry> LogEntries { get; }

        #endregion

        #region Option Properties

        /// <summary>
        /// Property Write 활성화
        /// </summary>
        public bool EnablePropertyWrite
        {
            get => _enablePropertyWrite;
            set
            {
                _enablePropertyWrite = value;
                OnPropertyChanged(nameof(EnablePropertyWrite));
            }
        }

        /// <summary>
        /// Selection Set 생성 활성화
        /// </summary>
        public bool EnableSelectionSetCreation
        {
            get => _enableSelectionSetCreation;
            set
            {
                _enableSelectionSetCreation = value;
                OnPropertyChanged(nameof(EnableSelectionSetCreation));
            }
        }

        /// <summary>
        /// TimeLiner Task 생성 활성화
        /// </summary>
        public bool EnableTimeLinerTaskCreation
        {
            get => _enableTimeLinerTaskCreation;
            set
            {
                _enableTimeLinerTaskCreation = value;
                OnPropertyChanged(nameof(EnableTimeLinerTaskCreation));
            }
        }

        /// <summary>
        /// 선택된 그룹화 전략
        /// </summary>
        public GroupingStrategy SelectedGroupingStrategy
        {
            get => _selectedGroupingStrategy;
            set
            {
                _selectedGroupingStrategy = value;
                OnPropertyChanged(nameof(SelectedGroupingStrategy));
            }
        }

        /// <summary>
        /// 그룹화 전략 목록
        /// </summary>
        public IEnumerable<GroupingStrategy> GroupingStrategies =>
            Enum.GetValues(typeof(GroupingStrategy)).Cast<GroupingStrategy>();

        /// <summary>
        /// 최소 매칭 성공률 (%)
        /// </summary>
        public double MinMatchSuccessRate
        {
            get => _minMatchSuccessRate;
            set
            {
                _minMatchSuccessRate = Math.Max(0, Math.Min(100, value));
                OnPropertyChanged(nameof(MinMatchSuccessRate));
            }
        }

        /// <summary>
        /// Selection Set 루트 폴더
        /// </summary>
        public string SelectionSetRootFolder
        {
            get => _selectionSetRootFolder;
            set
            {
                _selectionSetRootFolder = value;
                OnPropertyChanged(nameof(SelectionSetRootFolder));
            }
        }

        /// <summary>
        /// TimeLiner 루트 폴더
        /// </summary>
        public string TimeLinerRootFolder
        {
            get => _timeLinerRootFolder;
            set
            {
                _timeLinerRootFolder = value;
                OnPropertyChanged(nameof(TimeLinerRootFolder));
            }
        }

        #endregion

        #region Commands

        public ICommand BrowseCsvCommand { get; }
        public ICommand ExecuteCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ValidateCommand { get; }
        public ICommand ClearResultsCommand { get; }
        public ICommand ClearExistingDataCommand { get; }

        #endregion

        #region Constructor

        public AWP4DViewModel()
        {
            _automationService = new AWP4DAutomationService();
            _validator = new AWP4DValidator();
            LogEntries = new ObservableCollection<LogEntry>();

            // 이벤트 연결
            _automationService.ProgressChanged += OnAutomationProgressChanged;

            // 명령 초기화
            BrowseCsvCommand = new RelayCommand(BrowseCsv);
            ExecuteCommand = new RelayCommand(async () => await ExecuteAsync(), () => CanExecute);
            CancelCommand = new RelayCommand(Cancel, () => IsRunning);
            ValidateCommand = new RelayCommand(ValidateCsv, () => HasCsvFile);
            ClearResultsCommand = new RelayCommand(ClearResults);
            ClearExistingDataCommand = new RelayCommand(ClearExistingData);

            StatusMessage = "Select a CSV file to start";
            CurrentPhase = "Ready";
        }

        #endregion

        #region Methods

        /// <summary>
        /// CSV 파일 찾아보기
        /// </summary>
        private void BrowseCsv()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "CSV 파일|*.csv|모든 파일|*.*",
                DefaultExt = "csv",
                Title = "Select Schedule CSV file"
            };

            if (openDialog.ShowDialog() == true)
            {
                CsvFilePath = openDialog.FileName;
                AddLog(LogLevel.Info, $"CSV 파일 선택됨: {CsvFileName}");
            }
        }

        /// <summary>
        /// CSV 파일 검증
        /// </summary>
        private void ValidateCsv()
        {
            if (!HasCsvFile) return;

            try
            {
                var options = BuildOptions();
                var result = _validator.ValidatePreConditions(CsvFilePath, options);

                if (result.IsValid)
                {
                    StatusMessage = "Validation passed";
                    AddLog(LogLevel.Info, "CSV 파일 검증 통과");

                    foreach (var info in result.InfoMessages)
                    {
                        AddLog(LogLevel.Info, info);
                    }
                }
                else
                {
                    StatusMessage = "Validation failed";
                    foreach (var error in result.Errors)
                    {
                        AddLog(LogLevel.Error, error.Message);
                    }
                }

                foreach (var warning in result.Warnings)
                {
                    AddLog(LogLevel.Warning, warning.Message);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Validation error: {ex.Message}";
                AddLog(LogLevel.Error, $"검증 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 파이프라인 실행
        /// </summary>
        private async Task ExecuteAsync()
        {
            if (!HasCsvFile || IsRunning) return;

            IsRunning = true;
            ProgressPercentage = 0;
            LogEntries.Clear();

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                AddLog(LogLevel.Info, IsDryRun ? "드라이런 모드로 시작" : "파이프라인 실행 시작");

                var options = BuildOptions();
                options.DryRun = IsDryRun;

                // 백그라운드에서 실행 (UI 스레드 블로킹 방지)
                var result = await Task.Run(() =>
                    _automationService.ExecutePipeline(CsvFilePath, options, _cancellationTokenSource.Token));

                LastResult = result;

                // 로그 동기화
                foreach (var log in result.Logs)
                {
                    AddLog(log.Level, log.Message, log.Phase);
                }

                if (result.Success)
                {
                    StatusMessage = IsDryRun ? "Dry run completed" : "Pipeline completed successfully";
                    AddLog(LogLevel.Info, StatusMessage);
                }
                else
                {
                    StatusMessage = $"Pipeline failed: {result.ErrorMessage}";
                    AddLog(LogLevel.Error, StatusMessage);
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Pipeline cancelled by user";
                AddLog(LogLevel.Warning, StatusMessage);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                AddLog(LogLevel.Error, $"파이프라인 오류: {ex.Message}");
            }
            finally
            {
                IsRunning = false;
                ProgressPercentage = 100;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 실행 취소
        /// </summary>
        private void Cancel()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                AddLog(LogLevel.Warning, "취소 요청됨...");
            }
        }

        /// <summary>
        /// 결과 초기화
        /// </summary>
        private void ClearResults()
        {
            LastResult = null;
            LogEntries.Clear();
            ProgressPercentage = 0;
            CurrentPhase = "Ready";
            StatusMessage = "Results cleared";
        }

        /// <summary>
        /// 기존 AWP 데이터 삭제
        /// </summary>
        private void ClearExistingData()
        {
            var result = MessageBox.Show(
                $"'{SelectionSetRootFolder}' 폴더의 Selection Set과 TimeLiner Task를 모두 삭제하시겠습니까?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var options = BuildOptions();
                var (deletedSets, deletedTasks) = _automationService.ClearExistingData(options);

                StatusMessage = $"Cleared: {deletedSets} sets, {deletedTasks} tasks";
                AddLog(LogLevel.Info, StatusMessage);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Clear error: {ex.Message}";
                AddLog(LogLevel.Error, $"데이터 삭제 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 옵션 객체 생성
        /// </summary>
        private AWP4DOptions BuildOptions()
        {
            return new AWP4DOptions
            {
                EnablePropertyWrite = EnablePropertyWrite,
                EnableSelectionSetCreation = EnableSelectionSetCreation,
                EnableTimeLinerTaskCreation = EnableTimeLinerTaskCreation,
                GroupingStrategy = SelectedGroupingStrategy,
                MinMatchSuccessRate = MinMatchSuccessRate,
                SelectionSetRootFolder = SelectionSetRootFolder,
                TimeLinerRootFolder = TimeLinerRootFolder
            };
        }

        /// <summary>
        /// 로그 추가
        /// </summary>
        private void AddLog(LogLevel level, string message, string source = null)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Phase = source ?? "AWP4D"
            };

            // UI 스레드에서 실행
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => LogEntries.Add(entry));
            }
            else
            {
                LogEntries.Add(entry);
            }

            System.Diagnostics.Debug.WriteLine($"[{entry.Level}] {entry.Message}");
        }

        /// <summary>
        /// 진행 상황 이벤트 핸들러
        /// </summary>
        private void OnAutomationProgressChanged(object sender, PipelineProgressEventArgs e)
        {
            // UI 스레드에서 실행
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ProgressPercentage = e.Progress;
                    CurrentPhase = GetPhaseName(e.Phase);
                    StatusMessage = e.Message;
                });
            }
            else
            {
                ProgressPercentage = e.Progress;
                CurrentPhase = GetPhaseName(e.Phase);
                StatusMessage = e.Message;
            }
        }

        /// <summary>
        /// 단계 이름 변환
        /// </summary>
        private string GetPhaseName(PipelinePhase phase)
        {
            switch (phase)
            {
                case PipelinePhase.Validation: return "Validation";
                case PipelinePhase.CsvParsing: return "CSV Parsing";
                case PipelinePhase.ObjectMatching: return "Object Matching";
                case PipelinePhase.PropertyWrite: return "Property Write";
                case PipelinePhase.SelectionSetCreation: return "Selection Set";
                case PipelinePhase.TimeLinerTaskCreation: return "TimeLiner Task";
                case PipelinePhase.PostValidation: return "Post Validation";
                case PipelinePhase.Complete: return "Complete";
                default: return phase.ToString();
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _automationService.ProgressChanged -= OnAutomationProgressChanged;
        }

        #endregion
    }
}
