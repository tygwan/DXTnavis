using DXBase.Services;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace DXrevit.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private string _apiServerUrl = string.Empty;
        private string _defaultUsername = string.Empty;
        private int _timeoutSeconds;
        private int _batchSize;

        public SettingsViewModel()
        {
            // 현재 설정 로드
            LoadCurrentSettings();

            // 커맨드 초기화
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        #region Properties

        public string ApiServerUrl
        {
            get => _apiServerUrl;
            set
            {
                _apiServerUrl = value;
                OnPropertyChanged();
            }
        }

        public string DefaultUsername
        {
            get => _defaultUsername;
            set
            {
                _defaultUsername = value;
                OnPropertyChanged();
            }
        }

        public int TimeoutSeconds
        {
            get => _timeoutSeconds;
            set
            {
                _timeoutSeconds = value;
                OnPropertyChanged();
            }
        }

        public int BatchSize
        {
            get => _batchSize;
            set
            {
                _batchSize = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        private void LoadCurrentSettings()
        {
            try
            {
                var settings = ConfigurationService.LoadSettings();
                ApiServerUrl = settings.ApiServerUrl;
                DefaultUsername = settings.DefaultUsername;
                TimeoutSeconds = settings.TimeoutSeconds;
                BatchSize = settings.BatchSize;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("설정 로드 실패", "DXrevit", ex);
                MessageBox.Show("설정을 로드하는데 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteSave()
        {
            return !string.IsNullOrWhiteSpace(ApiServerUrl) &&
                   !string.IsNullOrWhiteSpace(DefaultUsername) &&
                   TimeoutSeconds > 0 &&
                   BatchSize > 0;
        }

        private void ExecuteSave()
        {
            try
            {
                var settings = new ConfigurationService.AppSettings
                {
                    ApiServerUrl = ApiServerUrl,
                    DefaultUsername = DefaultUsername,
                    TimeoutSeconds = TimeoutSeconds,
                    BatchSize = BatchSize,
                    LogFilePath = ConfigurationService.LoadSettings().LogFilePath // 기존 로그 경로 유지
                };

                ConfigurationService.SaveSettings(settings);

                LoggingService.LogInfo("설정이 저장되었습니다", "DXrevit");
                MessageBox.Show("설정이 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                CloseWindow(true);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("설정 저장 실패", "DXrevit", ex);
                MessageBox.Show($"설정 저장에 실패했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
