using Autodesk.Revit.DB;
using DXBase.Services;
using DXBase.Utils;
using DXrevit.Services;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DXrevit.ViewModels
{
    public class SnapshotViewModel : INotifyPropertyChanged
    {
        private readonly Document _document;
        private readonly DataExtractor _dataExtractor;
        private readonly ApiDataWriter _apiDataWriter;

        private string _modelVersion = string.Empty;
        private string _description = string.Empty;
        private string _createdBy = string.Empty;
        private bool _isProcessing;
        private string _statusMessage = string.Empty;
        private int _progressValue;

        public SnapshotViewModel(Document document)
        {
            _document = document;
            _dataExtractor = new DataExtractor(document);
            _apiDataWriter = new ApiDataWriter();

            // 진행률 보고자 설정
            var progressReporter = new Utils.ProgressReporter((percentage, message) =>
            {
                ProgressValue = percentage;
                StatusMessage = message;
            });
            _dataExtractor.SetProgressReporter(progressReporter);

            // 기본값 설정
            var settings = ConfigurationService.LoadSettings();
            ModelVersion = IdGenerator.GenerateModelVersion(_document.ProjectInformation.Name);
            CreatedBy = settings.DefaultUsername ?? Environment.UserName;
            Description = $"{DateTime.Now:yyyy-MM-dd} 스냅샷";
            StatusMessage = "저장 준비 완료";

            // Dispatcher 초기화
            Utils.DispatcherHelper.Initialize(System.Windows.Application.Current.Dispatcher);

            // 커맨드 초기화
            SaveCommand = new RelayCommand(async () => await ExecuteSaveAsync(), CanExecuteSave);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        #region Properties

        public string ModelVersion
        {
            get => _modelVersion;
            set
            {
                _modelVersion = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public string CreatedBy
        {
            get => _createdBy;
            set
            {
                _createdBy = value;
                OnPropertyChanged();
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanInput));
            }
        }

        public bool CanInput => !IsProcessing;

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        private bool CanExecuteSave()
        {
            return !IsProcessing &&
                   !string.IsNullOrWhiteSpace(ModelVersion) &&
                   !string.IsNullOrWhiteSpace(CreatedBy);
        }

        private async Task ExecuteSaveAsync()
        {
            try
            {
                IsProcessing = true;
                ProgressValue = 0;

                // 1. 데이터 추출 (메인 스레드에서 동기적으로 실행)
                StatusMessage = "Revit 데이터 추출 중...";
                ProgressValue = 10;

                // Revit API는 메인 스레드에서만 호출 가능하므로 동기 실행
                var extractedData = _dataExtractor.ExtractAll(ModelVersion, CreatedBy, Description);

                ProgressValue = 50;

                // 2. 데이터 전송
                StatusMessage = "API 서버로 데이터 전송 중...";
                ProgressValue = 60;

                bool success = await _apiDataWriter.SendDataAsync(extractedData);

                ProgressValue = 100;

                if (success)
                {
                    StatusMessage = "스냅샷 저장 완료!";
                    MessageBox.Show(
                        $"스냅샷이 성공적으로 저장되었습니다.\n\n" +
                        $"버전: {ModelVersion}\n" +
                        $"객체 수: {extractedData.Objects.Count}\n" +
                        $"관계 수: {extractedData.Relationships.Count}",
                        "저장 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // 창 닫기
                    CloseWindow(true);
                }
                else
                {
                    StatusMessage = "저장 실패";
                    MessageBox.Show(
                        "스냅샷 저장에 실패했습니다.\n로그 파일을 확인해주세요.",
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

    /// <summary>
    /// 간단한 RelayCommand 구현
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public async void Execute(object parameter)
        {
            if (_executeAsync != null)
                await _executeAsync();
            else
                _execute?.Invoke();
        }
    }
}
